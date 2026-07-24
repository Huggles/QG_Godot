using Godot;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json.Serialization;

public partial class CountryState : StateObject
{
    [JsonIgnore] public CountryData StaticCountryData;  
    public string Name { get; set; }
    public string NameCamelCase { get; set; }
    public string Label { get; set; }
    public CountryType Type { get; set; }
    public bool IsSupply { get; set; }
    public StraightState StraightState;

    public Country Country { get { return (Country)Id; } }
    public List<string> Neighbors { get; set; }

    [JsonIgnore] public List<int> ConnectedCountryIds { get; set; } = new List<int>();
    [JsonIgnore] public List<CountryState> ConnectedCountryStates => CountryState.ForIds(ConnectedCountryIds).ToList();

    public Dictionary<Faction, int> Units { get; set; } = new Dictionary<Faction, int>();

    public bool IsLand => Type == CountryType.LAND;
    public bool IsSea => Type == CountryType.SEA;

    public bool IsCountryEmpty => Units.Count == 0;
    public bool IsCountryFull => Units.Count == 3;

    public List<Faction> OccupyingFactions => Units.Keys.ToList();

    public FactionTeam OccupyingTeam => OccupyingFactions.Count == 0 
            ? FactionTeam.NONE
            : StaticGameData.FactionTeamForFaction(OccupyingFactions[0]);

    [JsonIgnore] public CountryScene CountryScene => NodeUtilities.Instance.CountriesNode.GetChildren().ToList().Find(c => c.Name == Name) as CountryScene ?? throw new Exception($"Couldn't find CountryScene for country: {Name}");

    public CountryState(CountryData countryData)
    {
        StaticCountryData = countryData;
        Id = countryData.Number;
        Name = countryData.UniqueName;
        NameCamelCase = countryData.UniqueNameCamelCase;
        Label = countryData.Label;
        Type = countryData.Type == 0 ? CountryType.LAND : CountryType.SEA;
        IsSupply = countryData.IsSupply == "true";
        Neighbors = new List<string>(countryData.Neighbors);

        // Set country type tag (faction-independent, so use Faction.ALL)
        if (Type == CountryType.LAND)
            this.Tags.AddForAll(Tag.LandCountry);
        else
            this.Tags.AddForAll(Tag.SeaCountry);

        // Presentation side-effect: drive the country's clickable visual off the Clickable tag.
        // Skipped on a headless server, which has no CountryScene (the getter would throw).
        if (!GameContext.IsHeadless)
        {
            this.Tags.TagAdded += (Tag t, Faction f) =>
            {
                if(t is Tag.Clickable) CountryScene.SetClickable();
            };
            this.Tags.TagRemoved += (Tag t, Faction f) =>
            {
                if(t is Tag.Clickable) CountryScene.SetUnclickable();
            };
        }
    }

    public void InitNeighborCountryStateArray()
    {
        ConnectedCountryIds = Neighbors
            .Select(neighbor => MultiplayerSession.Instance.GameState.CountryStateByName.TryGetValue(neighbor, out CountryState neighborState) 
                                ? neighborState.Id
                                : throw new Exception( $"Couldn't find: {neighbor} as neighbor of {Name}"))
            .ToList();
                 
       
    }

    public List<StraightState> ControllingStraightStates => IsSea ? [] : ConnectedCountryStates
        .Where(ccs => GameAPI.StraightStateForNeighbors(Id, ccs.Id) != null && GameAPI.StraightStateForNeighbors(Id, ccs.Id).ControllingCountryState.Id == Id)
        .Select(ccs => GameAPI.StraightStateForNeighbors(Id, ccs.Id))
        .ToList();
    
    private List<StraightState> ControlledByStraightStates => IsLand ?  new List<StraightState>() : ConnectedCountryStates.Where(ccs => IsSea && ccs.IsSea && GameAPI.StraightStateForNeighbors(Id, ccs.Id) != null).Select(ccs => GameAPI.StraightStateForNeighbors(Id, ccs.Id)).ToList();
    public List<StraightState> ControllingStraightStatesForFaction(Faction faction) => ControlledByStraightStates.Where(ss => ss.IsControlledByFaction(faction)).ToList();
    public List<StraightState> UncontrolledStraightStatesForFaction(Faction faction) => ControlledByStraightStates.Where(ss => !ss.IsControlledByFaction(faction)).ToList();
    public bool IsControlledByStraightState => ControlledByStraightStates.Count > 0;

    public List<int> AdjacentCountryIds(Faction faction) {        
        List<StraightState> _uncontrolledStraightStatesForFaction = UncontrolledStraightStatesForFaction(faction);
        return !IsControlledByStraightState 
        ? ConnectedCountryStates.Select(ccs => ccs.Id).ToList() 
        : ConnectedCountryStates.Where(
            ccs => !_uncontrolledStraightStatesForFaction.Any(uss => uss.IsForIds(this.Id, ccs.Id)))
            .Select(ccs => ccs.Id).ToList();      
    }
    

    public List<CountryState> AdjacentCountryStates(Faction faction) => CountryState.ForIds(AdjacentCountryIds(faction));

    public bool HasHarbor(Faction faction) => ConnectedCountryStates.Any(connected => connected.IsLand && connected.OccupyingTeam == StaticGameData.FactionTeamForFaction(faction));    
    public bool CanBuild(Faction faction) =>    
            CanRecruit(faction) && //Can faction recruit here (i.e., it's empty or occupied by their own team) AND
            !HasUnit(faction) && //No unit of theirs is already here AND
            (!IsHomeSpace(faction) ? HasAdjacentSuppliedUnit(faction) : true) && //If it's not their home space, they must have an adjacent supplied unit AND
            (this.IsSea ? HasHarbor(faction) : true); //If it's a sea country, they must have a harbor (i.e., an adjacent land country occupied by their team)

    public bool CanRecruit(Faction faction) => OccupyingTeam == FactionTeam.NONE || (OccupyingTeam == StaticGameData.FactionTeamForFaction(faction) && !HasUnit(faction));
    public bool HasUnit(Faction faction) => Units.ContainsKey(faction);
    public bool IsHomeSpace(Faction faction) => FactionState.ForEnum(faction).FactionData.HomeSpaceCountryState.Id == this.Id;
    public List<int> AdjacentUnits(Faction faction) => AdjacentCountryStates(faction).Where(ccs => ccs.Units.ContainsKey(faction)).Select(ccs => ccs.Units[faction]).ToList();
    public List<int> AdjacentSuppliedUnits(Faction faction) => UnitState.ForIds(AdjacentUnits(faction)).Where(unit => unit.InSupply).Select(unit => unit.Id).ToList();
    public bool HasAdjacentSuppliedUnit(Faction faction) => AdjacentSuppliedUnits(faction).Count > 0;
    public bool CanAttack(Faction faction) => HasAdjacentSuppliedUnit(faction) && HasAttackableUnit(faction);

    
    public List<BattleTarget> AdjacentBattleTargets(Faction attackingFaction, CountryType countryType)
    {
        List<BattleTarget> targets = new List<BattleTarget>();
        List<BattleTarget> targetableUnitIds = AdjacentCountryStates(attackingFaction)
            .Where(connectedCountryState => connectedCountryState.Type == countryType && connectedCountryState.CanAttack(attackingFaction)).ToList()
            .SelectMany(countryWithUnits => countryWithUnits.Units.Values.Where(unitId=>!UnitState.ForId(unitId).ImmuneForTurn)).Distinct().ToList()
            .Map(unitId => new BattleTarget(unitId, TargetType.UNIT));
        List<BattleTarget> targetableEmptyCountriesIds = AdjacentCountryStates(attackingFaction)
        .Where(connectedCountryState => connectedCountryState.Type == countryType && connectedCountryState.CanAttackWhenEmpty(attackingFaction)).ToList().ToCountryIds()
        .Map(countryId => new BattleTarget(countryId, TargetType.COUNTRY));
        targets.AddRange(targetableUnitIds);
        targets.AddRange(targetableEmptyCountriesIds);
        return targets;
    }
    public List<BattleTarget> BattleTargets(Faction attackingFaction)
    {
        if (!HasAdjacentSuppliedUnit(attackingFaction) || OccupyingTeam == StaticGameData.FactionTeamForFaction(attackingFaction))
        {
            return [];
        }
        else if (IsCountryEmpty)
        {
            return [new BattleTarget(Id, TargetType.COUNTRY)];
        }
        else
        {
            return Units.Values.ToList().Map(unitId => new BattleTarget(unitId, TargetType.UNIT));
        }
    }

    public bool HasAttackableUnit(Faction faction) => OccupyingTeam == StaticGameData.OpponentFactionTeamForFaction(faction);
    public bool CanAttackWhenEmpty(Faction faction) => HasAdjacentSuppliedUnit(faction) && IsCountryEmpty;

    /**
    * Static helpers
    */
    public static List<CountryState> AllCountryStates => GameSession.Current.GameState.CountryStateById.Values.ToList();
    public static CountryState ForId(int id) => GameSession.Current.GameState.CountryStateById.GetValueOrDefault(id) ?? null;

    public static List<CountryState> ForIds(IEnumerable<int> ids) => ids.Select(ForId).ToList();

    public static CountryState ForName(string name) => GameSession.Current.GameState.CountryStateByName.GetValueOrDefault(name) ?? null;
    public static List<CountryState> ForNames(List<string> names) => names.Map(n => GameSession.Current.GameState.CountryStateByName.GetValueOrDefault(n) ?? null);
    public static List<CountryState> ForUnitIds(List<int> unitIds) => ForIds(UnitState.ForIds(unitIds).Select(u => u.CountryId));

    public static CountryState ForEnum(Country country) => ForEnums(new List<Country> { country })[0];
    public static List<CountryState> ForEnums(List<Country> countries) => ForIds(countries.ToList().Map(countryEnum => (int)countryEnum));


    /**
    * Tag helpers
    */
    public static List<CountryState> WithTag(Tag tag, Faction faction) => AllCountryStates.Where(cs => cs.Tags.Has(tag, faction)).ToList();
    public static List<CountryState> WithTags(Tag[] tags, Faction faction) => AllCountryStates.Where(cs => tags.All(tag => cs.Tags.Has(tag, faction))).ToList();    
    
    public static List<CountryState> BuildableLand(Faction faction) => 
        WithTags(new[] { Tag.Buildable, Tag.LandCountry }, faction);
    
    public static List<CountryState> BuildableSea(Faction faction) => 
        WithTags(new[] { Tag.Buildable, Tag.SeaCountry }, faction);
    
    public static List<CountryState> RecruitableLand(Faction faction) => 
        WithTags(new[] { Tag.Recruitable, Tag.LandCountry }, faction);
    
    public static List<CountryState> RecruitableSea(Faction faction) => 
        WithTags(new[] { Tag.Recruitable, Tag.SeaCountry }, faction);
    
    public static List<CountryState> AttackableLand(Faction faction) => 
        WithTags(new[] { Tag.Attackable, Tag.LandCountry }, faction);
    
    public static List<CountryState> AttackableSea(Faction faction) => 
        WithTags(new[] { Tag.Attackable, Tag.SeaCountry }, faction);

    // ID helpers
    public static List<int> BuildableLandIds(Faction faction) => 
        BuildableLand(faction).Select(c => c.Id).ToList();
    
    public static List<int> BuildableSeaIds(Faction faction) => 
        BuildableSea(faction).Select(c => c.Id).ToList();
    
    public static List<int> RecruitableLandIds(Faction faction) => 
        RecruitableLand(faction).Select(c => c.Id).ToList();
    
    public static List<int> RecruitableSeaIds(Faction faction) => 
        RecruitableSea(faction).Select(c => c.Id).ToList();
    
    public static List<int> AttackableLandIds(Faction faction) => 
        AttackableLand(faction).Select(c => c.Id).ToList();
    
    public static List<int> AttackableSeaIds(Faction faction) => 
        AttackableSea(faction).Select(c => c.Id).ToList();
    
    public static bool operator ==(CountryState countryState, Country country)
    {
        return countryState.Id == (int)country;
    }
    public static bool operator !=(CountryState countryState, Country country) {
        return countryState.Id != (int)country;
    }

    public override bool Equals(object obj)
    {
        if (obj is Country countryEnum)
        {
            return Id == (int)countryEnum;
        }
        else if (obj is CountryState countryState)
        {
            return Id == countryState.Id;
        }
        else
        {
            return false;
        }
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Name);
    }
}
