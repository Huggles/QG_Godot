using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class CountryState : StateObject
{
    public CountryData StaticCountryData;

    public int Id { get; set; }
    public string Name { get; set; }
    public string NameCamelCase { get; set; }
public string Label { get; set; }
    public CountryType Type { get; set; }
    public bool IsSupply { get; set; }

    public List<string> Neighbors { get; set; }
    public List<CountryState> NeighborCountryStates { get; set; } = new List<CountryState>();

    public StraightState StraightState;

    public Dictionary<Faction, int> Units  { get; set; } = new Dictionary<Faction, int>();

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

        EventBus.Instance.SetCountriesClickable += SetClickable;
        EventBus.Instance.SetAllCountriesUnclickable += SetUnclickable;
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

    public void SetClickable(int[] countryIds)
    {
        if (countryIds.Contains(Id))
        {
            Node.SetClickable();
        }
    }

    public void SetUnclickable()
    {
        Node.SetUnclickable();
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

        if (Type == CountryType.SEA)
        {
            var factionTeam = StaticGameData.FactionTeamForFaction(faction);
            canBuild &= NeighborCountryStates.Any(n =>
                n.Type == CountryType.LAND && n.OccupyingTeam == factionTeam);
        }

        return canBuild;
    }

    public bool CanRecruit(Faction faction)
    {
        return OccupyingTeam == FactionTeam.NONE || !OccupyingFactions.Contains(faction);
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

    public bool CanAttackWhenEmpty(Faction faction)
    {
        return InRangeForAttack(faction) && IsCountryEmpty;
    }

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

    public static List<CountryState> ForUnitIds(IEnumerable<int> unitIds)
    {
        var countryIds = UnitState.ForIds(unitIds).Select(u => u.CountryId);
        return ForIds(countryIds);
    }
}
