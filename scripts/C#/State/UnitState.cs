using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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
    
    [Export] public CountryState CountryState;
    // InSupply is computed from tags - tag is the source of truth
    public bool InSupply => Tags.Has(Tag.InSupply, Faction);
    [Export] public bool ImmuneForTurn { get; set; } = false;

    private int countryId = -1;
    public int CountryId
    {
        get { return countryId; }
        set
        {
            countryId = value;
            if (countryId >= 0)
            {
                this.CountryState = CountryState.ForId(CountryId);
            }

        }
    }
    public bool IsDeployedToCountry => CountryId >= 0;

    public bool IsArmy => Type == UnitType.ARMY;
    public bool IsNavy => Type == UnitType.NAVY;

    public UnitScene Node { get; set; }
    public Callable ClickableCallback { get; set; }
    public FactionTeam FactionTeam { get { return StaticGameData.FactionTeamForFaction(Faction); } }

    public UnitState(UnitType type, Faction faction)
    {        
        Id = UnitPool.GetUniqueUnitId();
        Type = type;
        Faction = faction;

        EventBus.Instance.NewTurnStarted += (int turnNumber) => { this.ImmuneForTurn = false; };
    }

    public void InitNode()
    {
        Node = UnitScene.SpawnUnit(this);
        Node.Name += "_" + Id;
        NodeUtilities.Instance.UnitsNode.AddChild(Node, true);
    }

    public void Debug()
    {
        var debugStr = $"{Id} {Faction}";
        if (CountryState != null)
            debugStr += $" {CountryState.Label}";

        DebugUtilities.PrintPeer(debugStr);
    }

    public static UnitState ForId(int unitId)
    {
        return GameSession.Instance.GameState.UnitStatesById[unitId];
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
    public static List<UnitState> AllUnitStates => 
        GameSession.Instance.GameState.UnitStatesById.Values.ToList();
    
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
