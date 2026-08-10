using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Example scenario mutator: "after each play card step, remove one unit". The active faction chooses
/// which of its own units is lost.
///
/// The removal goes through the full reaction pipeline, so other factions can block it or respond to
/// it exactly as they could if a card had caused it.
/// </summary>
public class MutatorRemoveUnitAfterPlayCard : StepMutator
{
    public override TurnStep      Step        => TurnStep.PLAY_CARD;
    public override MutatorTiming Timing      => MutatorTiming.AFTER;
    public override string        Description => "Attrition: remove one of your units";
    public override string        BulletinText =>
        "After you play a card, the front consumes one of your armies. Remove a unit of your choice from the board.";

    public override async Task Run(Faction activeFaction)
    {
        List<int> unitIds = GameAPI.ActiveUnitsForFaction(activeFaction);

        // RemoveUnitChangeEvent resolves the unit's country in its base constructor, so it would throw
        // on an id that is not on the board. Nothing to do when the faction has no units deployed.
        if (unitIds.Count == 0) return;

        InputRequest response = await new InputRequest.SelectUnitRequestHandler(activeFaction, unitIds).BroadCast();
        if (response.ResponseUnitIds.Count == 0) return;

        await this.Do(new RemoveUnitChangeEvent(
            activeFaction, response.ResponseUnitIds[0], UnitRemovalReason.ELIMINATE));
    }
}
