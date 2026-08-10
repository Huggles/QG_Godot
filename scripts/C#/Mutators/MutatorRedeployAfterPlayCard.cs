using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Scenario mutator: "after each play card step, the active faction may redeploy one unit". The unit is
/// removed from the board and rebuilt as the same type anywhere that faction could build.
///
/// Both halves go through the full reaction pipeline, so opponents can block or respond to the removal
/// and to the rebuild independently — a block on the removal cancels the redeployment outright.
/// </summary>
public class MutatorRedeployAfterPlayCard : StepMutator
{
    public override TurnStep      Step        => TurnStep.PLAY_CARD;
    public override MutatorTiming Timing      => MutatorTiming.AFTER;
    public override string        Description => "Strategic redeployment: move a unit across the front";
    public override string        BulletinText =>
        "After you play a card, withdraw one of your units and rebuild it anywhere you could have built it.";

    public override async Task Run(Faction activeFaction)
    {
        List<int> unitIds = GameAPI.ActiveUnitsForFaction(activeFaction);

        // RemoveUnitChangeEvent resolves the unit's country in its base constructor, so it would throw
        // on an id that is not on the board. Nothing to redeploy when the faction has none deployed.
        if (unitIds.Count == 0) return;

        InputRequest unitResponse = await new InputRequest.SelectUnitRequestHandler(activeFaction, unitIds).BroadCast();
        if (unitResponse.ResponseUnitIds.Count == 0) return;

        int unitId = unitResponse.ResponseUnitIds[0];

        // Read the type before the removal. UnitState.Type survives it (only CountryId is cleared), but
        // it is what decides land vs sea below, so capture it while the unit is unambiguously on the board.
        bool isNavy = UnitState.ForId(unitId).IsNavy;

        RemoveUnitChangeEvent removeEvent =
            new RemoveUnitChangeEvent(activeFaction, unitId, UnitRemovalReason.ELIMINATE);
        await this.Do(removeEvent);

        // Blocked withdrawal means the unit never left, so there is nothing to rebuild.
        if (removeEvent.IsBlocked) return;

        // Deliberately computed after the removal has fully resolved, reactions included: GameAPI
        // .DeployUnitToCountry validates against the live Tag.Buildable and throws otherwise, and the
        // removal itself changes that set — most obviously by freeing the country just vacated, which a
        // pre-removal list could never offer (CanBuild requires !HasUnit(faction)).
        List<int> destinationIds = isNavy
            ? CountryState.BuildableSeaIds(activeFaction)
            : CountryState.BuildableLandIds(activeFaction);

        if (destinationIds.Count == 0)
        {
            DebugUtilities.PrintPeer(
                $"{activeFaction} has nowhere to rebuild the withdrawn unit — it is lost");
            return;
        }

        // A skip here throws StepSkippedException out of Run and the unit stays gone: the withdrawal is
        // already applied and replicated, so it cannot be taken back. Committing to the removal is the
        // decision point; the rebuild is not optional.
        int countryId = (await new InputRequest.SelectCountryRequestHandler(activeFaction, destinationIds)
            .BroadCast()).ResponseCountryIds[0];

        // BUILD, matching the Buildable set the destinations came from — RECRUIT would validate against
        // a different tag. As with StatusSyntheticFuel, a block-reaction card played against this deploy
        // could in principle invalidate the chosen country before it applies; GameAPI throws in that case.
        await this.Do(new DeployUnitChangeEvent(activeFaction, countryId, DeployType.BUILD));
    }
}
