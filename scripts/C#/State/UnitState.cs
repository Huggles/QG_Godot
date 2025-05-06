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

    public int Id { get; set; }
    public UnitType Type { get; set; }
    public Faction Faction;

    public int CountryId { get; set; } = -1;

    public bool IsDeployedToCountry => CountryId >= 0;

    public CountryState CountryState =>
        CountryId >= 0 ? GameSession.Instance.GameState.CountryStateById[CountryId] : null;

    public bool InSupply { get; set; } = false;

    public bool IsArmy => Type == UnitType.ARMY;
    public bool IsNavy => Type == UnitType.NAVY;

    public UnitScene Node { get; set; }
    public Callable ClickableCallback { get; set; }

    public UnitState(UnitType type, Faction faction)
    {
        Id = UnitPool.GetUniqueUnitId();
        Type = type;
        Faction = faction;

        //EventBusLocal.SetUnitsClickable += SetClickable;
        //EventBusLocal.SetAllUnitsUnclickable += SetUnclickable;
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
            Node.SetClickable(Callable.From((UnitScene unitScene) =>
            {
                SetUnclickable();
                EventBus.Emit("UnitClicked", Id);                
            }));
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

    public bool CanAttack(Faction againstFaction)
    {
        if (!IsDeployedToCountry) return false;

        foreach (var connectedCountry in CountryState.ConnectedCountries(againstFaction))
        {
            if (connectedCountry.OccupyingFactions.Contains(againstFaction))
                return true;
        }

        return false;
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
