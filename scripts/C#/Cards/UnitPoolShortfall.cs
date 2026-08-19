using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// The per-faction unit counts in QGData_Factions_V2.json are hard caps: every UnitState exists from
/// InstantiateUnitStates() onward, and "in the pool" just means CountryId == -1. So a faction can be
/// asked to deploy with nothing left to deploy — this is what happens then. The faction takes one of
/// its own units of that type off the board, which returns the piece to the pool, and the deploy
/// proceeds with it.
///
/// Deliberately NOT solved by gating CountryState.CanBuild / CanRecruit (and so Tag.Buildable /
/// Tag.Recruitable) on pool availability: that would make build and recruit cards silently unplayable
/// at zero pool instead of prompting, which is the opposite of the rule.
///
/// Host-only, and only reachable from the two DoChangeEvent paths. Scenario setup applies its deploys
/// with a bare Apply() and cannot ask anybody anything, so it never arrives here — it hits the
/// GameRuleException in UnitPool.GetAvailableUnitForFaction instead, with a pre-check in
/// GameModeMultiplayerDefault.DeployUnits to fail earlier and with a better message.
/// </summary>
public static class UnitPoolShortfall
{
    /// <summary>
    /// Free a piece for <paramref name="deploy"/> if the faction's pool for that unit type is empty.
    /// A no-op in the overwhelmingly common case, so it is cheap to call before every deploy.
    ///
    /// Must run AFTER the deploy's block window and BEFORE its Apply(): a block reaction that cancels
    /// the deploy has to mean no unit was ever recalled.
    /// </summary>
    public static async Task ResolveBeforeDeploy(DeployUnitChangeEvent deploy)
    {
        Faction faction = deploy.TriggeringFaction;
        UnitType unitType = deploy.UnitType;              // derived from the destination country's type

        if (UnitPool.FactionHasAvailableUnits(faction, unitType)) return;

        List<int> candidates = UnitPool.RecallCandidates(
            faction, unitType, deploy.CountryState, deploy.DeploymentType);

        // Pool empty AND nothing on the board means the cap for this type is 0 — a data problem, not a
        // game situation, so there is no prompt that could resolve it.
        if (candidates.Count == 0)
        {
            throw new GameRuleException(
                $"{faction} must free a {unitType} to deploy to " +
                $"{deploy.CountryState.Label} but has none deployed. Check NumberOfArmyUnits / " +
                $"NumberOfNavyUnits for {faction} in QGData_Factions_V2.json.");
        }

        // Explains the prompt that is about to appear. A PresentationEvent, so every peer sees why the
        // deploying faction is being asked something extra.
        await new ShowActionLabelPresentationEvent(
            faction, $"No {unitType} left in the pool — remove one of your {unitType} units first").Apply();

        // allowSkip: false — the removal is mandatory, so the prompt shows no Skip button. BroadCast can
        // still throw StepSkippedException (input timeout resolved as Skip, or error recovery releasing
        // the awaiter); that abandons the deploy, leaving the event registered but unapplied, which is
        // the same state a blocked event leaves behind. CardStep.Execute and StepMutatorRunner already
        // catch it.
        int unitId = (await new InputRequest.SelectUnitRequestHandler(faction, candidates, allowSkip: false)
            .BroadCast()).ResponseUnitIds[0];

        // No SourceCardId: the recall is not the card's doing and nothing may tie it back to one.
        //
        // Apply() rather than CardPlayPool.DoChangeEvent — it still broadcasts, hashes and animates, but
        // opens no pipeline. Both flags matter and neither is redundant: IsTrigger closes the block and
        // after-reaction windows, RegisterInPool keeps the event out of ChangeEventsPool (and out of
        // LastChangeEvent), so no pool-scoped Condition can ever see it. A recall is bookkeeping, not a
        // game event.
        await new RemoveUnitChangeEvent(faction, unitId, UnitRemovalReason.RECALL)
        {
            IsTrigger = false,
            RegisterInPool = false,
        }.Apply();
    }
}
