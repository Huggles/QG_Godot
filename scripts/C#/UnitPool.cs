using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class UnitPool : Object
{
    private static int unitCounter = -1;

    private static MultiplayerGameState gameState { get { return GameSession.Current.GameState; } }

    /// <summary>
    /// The next undeployed unit of this type, i.e. the piece a deploy will use.
    ///
    /// Scan order is load-bearing in multiplayer: DeployUnitChangeEventDto carries the country but NOT
    /// the unit id, so every peer runs this itself and they must agree. AllUnits projects
    /// GameState.UnitStates in creation order on every peer, which is what makes them agree.
    ///
    /// Throws rather than returning -1 when the pool is empty. Callers that CAN ask the player to free
    /// a piece must go through <see cref="UnitPoolShortfall.ResolveBeforeDeploy"/> first — reaching
    /// this throw means nobody could be asked (scenario setup) or a caller skipped that guard.
    /// </summary>
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

        int cap = unitStates.Count(unit => unit.Type == unitType);
        throw new GameRuleException(
            $"{faction} has no {unitType} left in its pool: all {cap} are deployed " +
            $"(the cap comes from QGData_Factions_V2.json). No unit was freed first, so either this " +
            $"deploy happened during game setup — where the player cannot be asked to remove one — or " +
            $"it bypassed UnitPoolShortfall.ResolveBeforeDeploy.");
    }

    public static int GetUniqueUnitId()
    {
        unitCounter += 1;
        return unitCounter;
    }

    /// <summary>
    /// Put the id stream back to its starting position. Called once per game, before
    /// InstantiateUnitStates runs — see MultiplayerSession.StartSession.
    ///
    /// Unlike GameMessage.ResetStream this IS needed for correctness, and a save is what makes it so.
    /// The counter is a process static, so a second game in the same process would otherwise number its
    /// pool from where the previous game stopped, and a restore into that shell would look up the unit
    /// ids recorded in the log against a pool that no longer contains them. That is why loading worked
    /// from a fresh client and threw KeyNotFoundException after quitting a session first.
    /// </summary>
    public static void ResetIdStream() => unitCounter = -1;

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

    /// <summary>
    /// The faction's own deployed units it may take off the board to free a piece for a deploy into
    /// <paramref name="destination"/>. Same type only — the pool is per type, so freeing an army does
    /// nothing for a navy.
    ///
    /// One exclusion. A BUILD into a country that is not the faction's home space needs
    /// <c>HasAdjacentSuppliedUnit</c> (CountryState.CanBuild), so removing the destination's ONLY
    /// adjacent supplied unit would make the destination unbuildable and the deploy would then throw
    /// out of GameAPI with the piece already gone. RECRUIT needs no exclusion: CanRecruit reads only
    /// the destination's occupying team and whether the faction already has a unit there, and the
    /// faction removing its own unit elsewhere changes neither.
    ///
    /// This catches the direct adjacency case exactly. A removal that instead breaks the supply CHAIN
    /// feeding an adjacent unit is not caught — the destination goes unbuildable and
    /// GameAPI.DeployUnitToCountry throws GameAPIException, which is a GameRuleException and so
    /// recoverable (popup + Continue). Detecting that properly would need a full
    /// GameStateCalculator.CalculateAll() per candidate, and CalculateAll broadcasts a
    /// RecalculateTagsMessage to every client, so it cannot be used as a speculative probe.
    /// </summary>
    public static List<int> RecallCandidates(Faction faction, UnitType unitType, CountryState destination, DeployType deployType)
    {
        List<int> deployed = UnitState.ForIds(FactionState.ForEnum(faction).ActiveUnitIds)
            .Where(unit => unit.Type == unitType)
            .Select(unit => unit.Id)
            .ToList();

        bool needsAdjacentSupply = deployType == DeployType.BUILD && !destination.IsHomeSpace(faction);
        if (!needsAdjacentSupply) return deployed;

        List<int> support = destination.AdjacentSuppliedUnits(faction);
        if (support.Count != 1) return deployed;

        List<int> filtered = deployed.Where(id => id != support[0]).ToList();

        // Never hand back an empty list while there is still something to remove: a choice the player
        // can make beats a hard stop, even when it may cost them the deploy.
        return filtered.Count > 0 ? filtered : deployed;
    }
}
