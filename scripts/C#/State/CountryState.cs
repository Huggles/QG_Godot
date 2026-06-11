using Godot;
using System;
using System.Collections.Generic;
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
    [JsonIgnore] public List<CountryState> NeighborCountryStates { get; set; } = new List<CountryState>();

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

        this.Tags.TagAdded += (Tag t, Faction f) =>
        {
            if(t is Tag.Clickable) CountryScene.SetClickable();
        };
        this.Tags.TagRemoved += (Tag t, Faction f) =>
        {
            if(t is Tag.Clickable) CountryScene.SetUnclickable();
        };
    }

    public void InitNeighborCountryStateArray()
    {
        foreach (var neighbor in Neighbors)
        {
            if (MultiplayerSession.Instance.GameState.CountryStateByName.TryGetValue(neighbor, out CountryState neighborState))
            {
                NeighborCountryStates.Add(neighborState);
            }

            else
            {
                string exceptionMessage = $"Couldn't find: {neighbor} as neighbor of {Name}";
                throw new Exception(exceptionMessage);
            }

        }
    }

    public List<int> ConnectedCountryIds(Faction faction)
    {
        return ConnectedCountries(faction).Select(c => c.Id).ToList();
    }

    public List<CountryState> ConnectedCountries(Faction faction)
    {
        return NeighborCountryStates.Where(neighbor =>
        {
            if (IsSea && neighbor.IsSea)
            {
                var straight = GameAPI.StraightStateForNeighbors(Id, neighbor.Id);
                return straight == null || straight.ControllingCountryState.OccupyingTeam == StaticGameData.FactionTeamForFaction(faction);
            }
            return true;
        }).ToList();
    }

    public bool HasHarbor(Faction faction)
    {
        if (!IsSea) return false;

        return ConnectedCountries(faction).Any(connected =>
            connected.IsLand && connected.OccupyingTeam == StaticGameData.FactionTeamForFaction(faction));
    }

    public void DeployUnit(Faction faction, UnitType unitType, DeployType deployType)
    {
        int unitId = UnitPool.GetAvailableUnitForFaction(faction, unitType);
        if(unitId == -1) throw new Exception($"No available units of type {unitType} for faction {faction}");
        var unit = UnitState.ForId(unitId);

        bool deployable = deployType != DeployType.BUILD || CanBuild(faction);
        if (!IsCountryFull && deployable)
        {
            Units[faction] = unit.Id;
            unit.CountryId = Id;
        }
        EventBus.Emit(EventBus.SignalName.UnitDeployed, unit.Id, Id);
        
    }

    public void RemoveUnit(int unitId)
    {
        UnitState unit = UnitState.ForId(unitId);
        Units.Remove(unit.Faction);
        unit.CountryId = -1;
        EventBus.Emit(EventBus.SignalName.UnitRemoved, unitId, Id);
    }

    public bool CanBuild(Faction faction)
    {
        
        bool canBuild = CanRecruit(faction) &&
                        !OccupyingFactions.Contains(faction) &&
                        OccupyingTeam != StaticGameData.OpponentFactionTeamForFaction(faction);
        if (!(FactionState.ForEnum(faction).FactionData.HomeSpaceCountryState.Id == this.Id))
        {
            canBuild &= NeighborCountryStates.Where(neighborCountryState => neighborCountryState.Units.ContainsKey(faction) && UnitState.ForId(neighborCountryState.Units[faction]).InSupply).ToList().Count > 0;
        }

        if (Type == CountryType.SEA)
        {
            FactionTeam factionTeam = StaticGameData.FactionTeamForFaction(faction);
            canBuild &= NeighborCountryStates.Any(n =>
                n.Type == CountryType.LAND && n.OccupyingTeam == factionTeam);
        }

        return canBuild;
    }

    public bool CanRecruit(Faction faction)
    {
        return OccupyingTeam == FactionTeam.NONE || (OccupyingTeam == StaticGameData.FactionTeamForFaction(faction) && !OccupyingFactions.Contains(faction));
    }

    public bool InRangeForAttack(Faction faction)
    {
        foreach (var ccs in ConnectedCountries(faction))
        {
            if (ccs.Units.TryGetValue(faction, out int unitId) &&
                UnitState.ForId(unitId).InSupply)
            {
                return true;
            }
        }
        return false;
    }

    public bool CanAttack(Faction faction)
    {
        return InRangeForAttack(faction) && HasAttackableTarget(faction);
    }

    public bool HasAdjacentSuppliedUnit(Faction faction)
    {
        return ConnectedCountries(faction).Any(connectedCountryState => connectedCountryState.OccupyingFactions.Contains(faction) && UnitState.ForId(connectedCountryState.Units[faction]).InSupply);
    }

    // public List<BattleTarget> AdjacentBattleTargets(Faction attackingFaction)
    // {
    //     List<BattleTarget> targets = new List<BattleTarget>();
    //     targets.AddRange(AdjacentBattleTargets(attackingFaction, CountryType.LAND));
    //     targets.AddRange(AdjacentBattleTargets(attackingFaction, CountryType.SEA));
    //     return targets;

    // }
    public List<BattleTarget> AdjacentBattleTargets(Faction attackingFaction, CountryType countryType)
    {
        List<BattleTarget> targets = new List<BattleTarget>();
        List<BattleTarget> targetableUnitIds = ConnectedCountries(attackingFaction)
            .Where(connectedCountryState => connectedCountryState.Type == countryType && connectedCountryState.CanAttack(attackingFaction)).ToList()
            .SelectMany(countryWithUnits => countryWithUnits.Units.Values.Where(unitId=>!UnitState.ForId(unitId).ImmuneForTurn)).Distinct().ToList()
            .Map(unitId => new BattleTarget(unitId, TargetType.UNIT));
        List<BattleTarget> targetableEmptyCountriesIds = ConnectedCountries(attackingFaction)
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

    public bool HasAttackableTarget(Faction faction)
    {
        return OccupyingTeam == StaticGameData.OpponentFactionTeamForFaction(faction);
    }

    public bool CanAttackWhenEmpty(Faction faction)
    {
        return InRangeForAttack(faction) && IsCountryEmpty;
    }


    public static List<CountryState> AllCountryStates => GameSession.Current.GameState.CountryStateById.Values.ToList();

    public static CountryState ForId(int id)
    {
        return GameSession.Current.GameState.CountryStateById.GetValueOrDefault(id) ?? null;
    }

    public static List<CountryState> ForIds(IEnumerable<int> ids)
    {
        return ids.Select(ForId).ToList();
    }

    public static CountryState ForName(string name)
    {
        return GameSession.Current.GameState.CountryStateByName.GetValueOrDefault(name) ?? null;
    }
    public static List<CountryState> ForNames(List<string> names)
    {
        return names.Map(n => GameSession.Current.GameState.CountryStateByName.GetValueOrDefault(n) ?? null);
    }

    public static List<CountryState> ForUnitIds(IEnumerable<int> unitIds)
    {
        var countryIds = UnitState.ForIds(unitIds).Select(u => u.CountryId);
        return ForIds(countryIds);
    }

    public static CountryState ForEnum(Country country)
    {
        return ForEnums(new List<Country> { country })[0];
    }

    public static List<CountryState> ForEnums(IEnumerable<Country> countries)
    {
        return ForIds(countries.ToList().Map(countryEnum => (int)countryEnum));
    }
    
    /// <summary>
    /// Returns all countries that have the specified tag for the given faction.
    /// Also matches tags set with Faction.ALL (general tags).
    /// </summary>
    public static List<CountryState> WithTag(Tag tag, Faction faction)
    {
        return AllCountryStates.Where(cs => cs.Tags.Has(tag, faction)).ToList();
    }
    
    /// <summary>
    /// Returns all countries that have ALL of the specified tags for the given faction.
    /// Automatically handles mixed queries: faction-specific tags (e.g., Tag.Buildable) 
    /// and general tags (e.g., Tag.LandCountry set with Faction.ALL).
    /// Example: WithTags(new[] { Tag.Buildable, Tag.LandCountry }, Faction.Germany)
    /// finds countries buildable by Germany that are also land countries.
    /// </summary>
    public static List<CountryState> WithTags(Tag[] tags, Faction faction)
    {
        return AllCountryStates.Where(cs => tags.All(tag => cs.Tags.Has(tag, faction))).ToList();
    }
    
    // Common query helpers
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
