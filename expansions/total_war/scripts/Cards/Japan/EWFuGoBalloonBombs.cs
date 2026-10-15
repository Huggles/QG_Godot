using System.Collections.Generic;
using System.Threading.Tasks;

public partial class EWFuGoBalloonBombs : EWCardLogic
{
    private const int CHOICE_RETURN = 0;
    private const int CHOICE_LEAVE  = 1;

    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.Fixed(_ => new ScorePointsChangeEvent(new VPEntry(1, "Fu-Go Balloon Bombs"), Faction)))
            .WithGuidance("Gain 1 Victory Point"),

        // The played card already sits in the discard pile, so every card in hand is one of the "3 other cards".
        new RequirementStep(this, async () => {
            var choice = await new InputRequest.SelectOptionRequestHandler(Faction,
                new List<string> { "Discard 3 cards to take Fu-Go Balloon Bombs back", "Leave it in the discard pile" },
                new List<int> { CHOICE_RETURN, CHOICE_LEAVE },
                "Fu-Go Balloon Bombs").BroadCast();
            if (choice.ResponseCardIds[0] != CHOICE_RETURN) throw new StepSkippedException();
            return new ForceDiscardHandCardsChangeEvent(Faction, Faction, 3);
        })
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => s.Board.ForFaction(Faction).Hand.Count >= 3), this))
        .WithGuidance("Discard 3 other cards to return this card to its hand"),

        // Deferred to the end of the step, as StatusWomenConscripts does: a card moved mid-play is re-armed, and DoCard would run it again.
        new EffectStep(this, async () => {
            ModifierRegistry.Register(new MutatorRecycleAfterStep(
                Faction,
                CardState.Id,
                CardState.Id,
                GameFlow.Instance.TurnStep,
                RecycleDestination.Hand,
                "Fu-Go Balloon Bombs: return the card to hand",
                "Three other cards were discarded in its place, so Fu-Go Balloon Bombs goes back to its owner's hand."));
            await Task.CompletedTask;
            return CardStepResult.Nothing;
        })
        .RequiringPreviousStep()
    };
}
