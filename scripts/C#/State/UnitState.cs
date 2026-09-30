using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

public partial class UnitState : StateObject
{
    [Signal] public delegate void BeforeUnitDeployedToCountryEventHandler();
    [Signal] public delegate void AfterUnitDeployedToCountryEventHandler();
    [Signal] public delegate void BeforeUnitRemovedFromCountryEventHandler(int unitId, int countryId);
    [Signal] public delegate void AfterUnitRemovedFromCountryEventHandler();
    [Signal] public delegate void UnitBecomesClickableEventHandler();
    [Signal] public delegate void UnitBecomesUnclickableEventHandler();
    
    [Export] public UnitType Type { get; set; }
    [Export] public Faction Faction;
    
    [JsonIgnore] public CountryState CountryState => CountryId >= 0 ? CountryState.ForId(CountryId) : null;
    // InSupply is computed from tags - tag is the source of truth
    public bool InSupply => BoardState.Live.InSupply(this);
    public bool ImmuneForTurn { get => Record.ImmuneForTurn; set => Record.ImmuneForTurn = value; }
    public bool SuppliedForTurn { get => Record.SuppliedForTurn; set => Record.SuppliedForTurn = value; }

    /// <summary>This unit's data on the live board.</summary>
    [JsonIgnore] public UnitRecord Record { get; } = new();
    [JsonIgnore] public override TagContainer Tags => Record.Tags;

    [JsonIgnore] public int CountryId { get => Record.CountryId; set => Record.CountryId = value; }
    public bool IsDeployedToCountry => CountryId >= 0;

    public bool IsArmy => Type == UnitType.ARMY;
    public bool IsNavy => Type == UnitType.NAVY;

    [JsonIgnore] public UnitScene UnitScene { get; set; }
    [JsonIgnore] public Callable ClickableCallback { get; set; }
    public FactionTeam FactionTeam { get { return StaticGameData.FactionTeamForFaction(Faction); } }

    public UnitState(UnitType type, Faction faction)
    {        
        Id = UnitPool.GetUniqueUnitId();
        Type = type;
        Faction = faction;

        // The per-turn reset of ImmuneForTurn/SuppliedForTurn lives in ChangeRoundChangeEvent, not on a
        // signal: both fields are in ComputeHash, and the signal is host-only.
    }

    

    public static UnitState ForId(int unitId)
    {
        return GameSession.Current.GameState.UnitStatesById[unitId];
    }

    public static List<UnitState> ForIds(IEnumerable<int> unitIds)
    {
        var result = new List<UnitState>();
        foreach (var id in unitIds)
        {
            result.Add(ForId(id));
        }
        return result;
    }
    
    // Helper methods for tag-based queries
    [JsonIgnore] public static List<UnitState> AllUnitStates => 
        GameSession.Current.GameState.UnitStatesById.Values.ToList();
    
    public static List<UnitState> WithTag(Tag tag, Faction faction) => BoardState.Live.UnitsWithTag(tag, faction);
    
    public static List<UnitState> AttackableArmies(Faction faction) => BoardState.Live.AttackableArmies(faction);
    
    public static List<UnitState> AttackableNavies(Faction faction) => BoardState.Live.AttackableNavies(faction);

    // ID helpers
    public static List<int> AttackableArmyIds(Faction faction) =>
        AttackableArmies(faction).Select(u => u.Id).ToList();
    
    public static List<int> AttackableNavyIds(Faction faction) =>
        AttackableNavies(faction).Select(u => u.Id).ToList();
}
