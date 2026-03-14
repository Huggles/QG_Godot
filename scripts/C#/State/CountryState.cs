using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class CountryState : StateObject
{
    [Export] public CountryData StaticCountryData;
    [Export] public string Name { get; set; }
    [Export] public string NameCamelCase { get; set; }
    [Export] public string Label { get; set; }
    [Export] public CountryType Type { get; set; }
    [Export] public bool IsSupply { get; set; }
    [Export] public StraightState StraightState;

    public Country Country { get { return (Country)Id; } }
    public List<string> Neighbors { get; set; }
    public List<CountryState> NeighborCountryStates { get; set; } = new List<CountryState>();


    public Dictionary<Faction, int> Units { get; set; } = new Dictionary<Faction, int>();

    public bool IsLand => Type == CountryType.LAND;
    public bool IsSea => Type == CountryType.SEA;

    public bool IsCountryEmpty => Units.Count == 0;
    public bool IsCountryFull => Units.Count == 3;

    public List<Faction> OccupyingFactions =>
        Units.Keys.ToList();

    public FactionTeam OccupyingTeam =>
        OccupyingFactions.Count == 0
            ? FactionTeam.NONE
            : StaticGameData.FactionTeamForFaction(OccupyingFactions[0]);

    public CountryScene Node;

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
            if(t is Tag.Clickable) Node.SetClickable();
        };
        this.Tags.TagRemoved += (Tag t, Faction f) =>
        {
            if(t is Tag.Clickable) Node.SetUnclickable();
        };
    }

    public void InitNeighborCountryStateArray()
    {
        foreach (var neighbor in Neighbors)
        {
            if (Game.GameState.CountryStateByName.TryGetValue(neighbor, out CountryState neighborState))
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

    public void InitNode()
    {
        Node = CountryScene.SpawnCountry(this);
        NodeUtilities.Instance.CountriesNode.AddChild(Node, false);
        Node.Position = StaticCountryData.WorldPositionCenter;
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
                var straight = GameStateUtilities.StraightStateForNeighbors(Id, neighbor.Id);
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

    public void DeployUnit()
    {
        // Logic to be implemented
    }

    public void RemoveUnit()
    {
        // Logic to be implemented
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


    public static List<CountryState> AllCountryStates => GameSession.Instance.GameState.CountryStateById.Values.ToList();

    public static CountryState ForId(int id)
    {
        return GameSession.Instance.GameState.CountryStateById[id];
    }

    public static List<CountryState> ForIds(IEnumerable<int> ids)
    {
        return ids.Select(ForId).ToList();
    }

    public static CountryState ForName(string name)
    {
        return GameSession.Instance.GameState.CountryStateByName[name];
    }
    public static List<CountryState> ForNames(List<string> names)
    {
        return names.Map(n => GameSession.Instance.GameState.CountryStateByName[n]);
    }

    public static List<CountryState> ForUnitIds(IEnumerable<int> unitIds)
    {
        var countryIds = UnitState.ForIds(unitIds).Select(u => u.CountryId);
        return ForIds(countryIds);
    }

    public static CountryState ForEnum(Country country)
    {
        DebugUtilities.PrintPeer(ForEnums(new List<Country> { country })[0].Name);
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
