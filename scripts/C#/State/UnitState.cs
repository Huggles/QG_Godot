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
    public bool InSupply => Tags.Has(Tag.InSupply, Faction);
    [Export] public bool ImmuneForTurn { get; set; } = false;

    public int CountryId = -1;
    public bool IsDeployedToCountry => CountryId >= 0;

    public bool IsArmy => Type == UnitType.ARMY;
    public bool IsNavy => Type == UnitType.NAVY;

    [JsonIgnore] public UnitScene Node { get; set; }
    [JsonIgnore] public Callable ClickableCallback { get; set; }
    public FactionTeam FactionTeam { get { return StaticGameData.FactionTeamForFaction(Faction); } }

    public UnitState(UnitType type, Faction faction)
    {        
        Id = UnitPool.GetUniqueUnitId();
        Type = type;
        Faction = faction;

        EventBus.Instance.NewTurnStarted += (int turnNumber) => { this.ImmuneForTurn = false; };
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
    
    public static List<UnitState> WithTag(Tag tag, Faction faction) =>
        AllUnitStates.Where(us => us.Tags.Has(tag, faction)).ToList();
    
    public static List<UnitState> AttackableArmies(Faction faction) =>
        AllUnitStates.Where(us => us.Tags.Has(Tag.Attackable, faction) && us.Type == UnitType.ARMY).ToList();
    
    public static List<UnitState> AttackableNavies(Faction faction) =>
        AllUnitStates.Where(us => us.Tags.Has(Tag.Attackable, faction) && us.Type == UnitType.NAVY).ToList();

    // ID helpers
    public static List<int> AttackableArmyIds(Faction faction) =>
        AttackableArmies(faction).Select(u => u.Id).ToList();
    
    public static List<int> AttackableNavyIds(Faction faction) =>
        AttackableNavies(faction).Select(u => u.Id).ToList();
}
