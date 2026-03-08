using Godot;
using System;
using System.Collections.Generic;

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
    [Export] public bool InSupply { get; set; } = false;
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

        EventBus.Instance.SetUnitsClickable += SetClickable;
        EventBus.Instance.SetAllUnitsUnclickable += SetUnclickable;
        EventBus.Instance.NewTurnStarted += (int turnNumber) => { this.ImmuneForTurn = false; };

        Tags.TagAdded += (Tag t) =>
        {
            DebugUtilities.PrintPeer("TAG ADDED");
            DebugUtilities.PrintPeer(t);
            if(t is Tag.Clickable) Node.SetClickable();
        };
        Tags.TagRemoved += (Tag t) =>
        {
            if(t is Tag.Clickable) Node.SetUnclickable();
        };
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

        GD.Print(debugStr);
    }

    public void SetClickable(int[] unitIds)
    {
        if (Array.IndexOf(unitIds, Id) >= 0)
        {
            Node.SetClickable();
        }
    }

    public void SetUnclickable()
    {
        Node.SetUnclickable();
    }

    public void SetInSupply()
    {
        InSupply = true;
        Node.HideOutOfSupply();
    }

    public void SetOutOfSupply()
    {
        InSupply = false;
        Node.ShowOutOfSupply();
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
}
