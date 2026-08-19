using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class UnitPool : Object
{
    private static int unitCounter = -1;

    private static MultiplayerGameState gameState { get { return GameSession.Current.GameState; } }

    public static int GetAvailableUnitForFaction(Faction faction, UnitType unitType)
    {        
        List<UnitState> unitStates = UnitState.ForIds(FactionState.ForEnum(faction).AllUnits);
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
        return AvailableUnitCount(faction, unitType) > 0;
    }

    /// <summary>
    /// How many units of this type the faction still has in the pool, i.e. not deployed to a country.
    /// Returns 0 before the game state exists, so UI can call this while it is still being built.
    /// </summary>
    public static int AvailableUnitCount(Faction faction, UnitType unitType)
    {
        FactionState factionState = FactionState.ForEnum(faction);
        if (factionState == null) return 0;

        return UnitState.ForIds(factionState.AllUnits)
            .Count(unit => unit.Type == unitType && !unit.IsDeployedToCountry);
    }
}
