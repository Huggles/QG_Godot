using System.Collections.Generic;

public partial class EventProductionInitiative : EventCardLogic
{
    // No Targets() override: the choice is among deck cards, which have no place on the board.

    private List<int> StatusCards(BoardState board) =>
        TotalWarCards.DeckChoices(board, Faction, card => card.CardType == CardType.STATUS);

    /// <summary>The Status card goes from deck to table without passing through hand, as Reallocate Resources plays its card.</summary>
    public override List<CardStep> OnActivate() => new()
    {
        new PlayCardStep(this, PlayChoice.From(c => StatusCards(c.Board), "Take a Status card from your draw deck and play it"))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => StatusCards(s.Board).Count > 0), this))
            .WithGuidance("Play a Status card of its choice from its draw deck"),

        TotalWarCards.ShuffleStep(this)
            .RequiringPreviousStep()
    };
}
