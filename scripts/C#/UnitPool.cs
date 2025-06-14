using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class UnitPool : Object
{
    private static int unitCounter = -1;

    private static GameState gameState { get { return GameSession.Instance.GameState; } }

    public static int GetAvailableUnitForFaction(Faction faction, UnitType unitType)
    {        
        List<UnitState> unitStates = UnitState.ForIds(GameSession.FactionStates[faction].AllUnits);
        foreach (var unitState in unitStates)
        {
            if (!unitState.IsDeployedToCountry && unitState.Type == unitType)
            {
                return unitState.Id;
            }
        }
        throw new NotSupportedException("COULDNT FIND UNIT IN UNIT POOL");        
    }

    public static int GetUniqueUnitId()
    {
        unitCounter += 1;
        return unitCounter;
    }

    public static bool FactionHasAvailableArmy(Faction faction)
    {
        return FactionHasAvailableUnits(faction, UnitType.ARMY);
    }

    public static bool FactionHasAvailableNavy(Faction faction)
    {
        return FactionHasAvailableUnits(faction, UnitType.NAVY);
    }

    public static bool FactionHasAvailableUnits(Faction faction, UnitType unitType)
    {
        List<UnitState> unitStates = UnitState.ForIds(GameSession.FactionStates[faction].AllUnits);

        var availableUnitStates = unitStates
            .Where(unit => unit.Type == unitType && !unit.IsDeployedToCountry)
            .ToList();

        return availableUnitStates.Count > 0;
    }
}
