using System.Collections.Generic;
using System.Linq;
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

        // A "build that army again" onto a country the faction already occupies consumes no pool piece:
        // GameAPI.DeployUnitToCountry redeploys the piece already standing there. An empty pool is no
        // obstacle, so asking the player to give up a unit would be taking one for nothing.
        if (deploy.CountryState.HasUnit(faction)) return;

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

        await RetargetIfRecallBrokeTheDestination(deploy);
    }

    /// <summary>
    /// The recall may have destroyed the very supply chain that made the destination legal. When it
    /// has, ask for a new destination rather than letting the deploy fail.
    ///
    /// Why this is needed at all: a BUILD onto a non-home space requires an adjacent supplied unit
    /// (CountryState.CanBuild), and supply is transitive. UnitPool.RecallCandidates excludes the
    /// destination's directly adjacent supporter, but a unit further back down the chain supports it
    /// just as much and is still offered — remove that one and the destination goes unbuildable
    /// between the prompt and the deploy. GameAPI.DeployUnitToCountry then throws GameAPIException,
    /// which is recoverable, so play continues — but the faction has already given up the unit and
    /// spent the card, and gets nothing for either. Measured at 8 of 120 headless games, always late
    /// (rounds 12-20) when the pool is empty and chains are long.
    ///
    /// No recalculation is triggered here: RemoveUnitChangeEvent takes the default RecalcScope.All, so
    /// the tags read below were already rebuilt by its Apply(). Adding another CalculateAll() would
    /// re-broadcast a RecalculateTagsMessage to every client for no new information.
    ///
    /// Safe to move the target at this point in the pipeline: ResolveBeforeDeploy runs after the block
    /// window and before the deploy's own Apply(), so nothing has been replicated yet — ToDto() reads
    /// CountryId when Apply() finally emits, and peers only ever see the final destination.
    /// </summary>
    private static async Task RetargetIfRecallBrokeTheDestination(DeployUnitChangeEvent deploy)
    {
        Faction faction = deploy.TriggeringFaction;
        CountryState destination = deploy.CountryState;
        DeployType deployType = deploy.DeploymentType;

        if (CanDeployTo(destination, faction, deployType)) return;

        // Same CountryType only. DeployUnitChangeEvent.UnitType is derived from the destination
        // (LAND -> ARMY, SEA -> NAVY), so a land-to-sea move would silently change which unit type is
        // being deployed — and the piece just recalled is of the original type.
        List<int> alternatives = CountryState.AllCountryStates
            .Where(country => country.Type == destination.Type
                              && CanDeployTo(country, faction, deployType))
            .Select(country => country.Id)
            .ToList();

        // Nothing legal anywhere: leave the target alone and let GameAPI throw as it did before. A
        // prompt offering no choice is worse than the error, and the error still names the country.
        if (alternatives.Count == 0) return;

        await new ShowActionLabelPresentationEvent(faction,
            $"{destination.Label} is no longer {(deployType == DeployType.BUILD ? "buildable" : "recruitable")} " +
            "after the recall — choose another space").Apply();

        // BroadCast throws StepSkippedException if the player skips, which abandons the deploy exactly
        // as a skipped recall already does. That leaves the recalled unit off the board — the same
        // outcome as today's failed deploy, so skipping is never worse than not offering the choice.
        deploy.CountryId = (await new InputRequest.SelectCountryRequestHandler(faction, alternatives)
            .BroadCast()).ResponseCountryIds[0];
    }

    /// <summary>
    /// Mirrors GameAPI.DeployUnitToCountry's own acceptance test, and exists so that the check above
    /// and the list of alternatives offered cannot drift apart: every country offered is one GameAPI
    /// will accept, and the destination is only replaced when GameAPI would have rejected it.
    /// </summary>
    private static bool CanDeployTo(CountryState country, Faction faction, DeployType deployType)
    {
        // A country the faction already occupies is a rebuild in place: GameAPI redeploys the piece
        // standing there, so fullness cannot block it — the slot being filled is the one being vacated.
        bool rebuildInPlace = country.Units.ContainsKey(faction);

        bool deployable = deployType == DeployType.BUILD
            ? country.Tags.Has(Tag.Buildable, faction)
            : country.Tags.Has(Tag.Recruitable, faction);

        return deployable && (!country.IsCountryFull || rebuildInPlace);
    }
}
