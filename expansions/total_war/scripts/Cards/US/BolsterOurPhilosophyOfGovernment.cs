using System.Collections.Generic;
using System.Linq;

/// <summary>
/// "Use at the beginning of your Draw step. Discard the top 2 cards of your draw deck to allow the United Kingdom to place a
/// Response card from its discard pile face down on the table." The US player picks the card; the UK's discard pile is public.
/// </summary>
public partial class BolsterOurPhilosophyOfGovernment : BolsterCardLogic
{
    private bool CanPlace(BoardState board) => TotalWarCards.DiscardedResponses(board, Faction.UNITED_KINGDOM, this).Count > 0;

    protected override List<Condition> CardTriggers() => TotalWarCards.AtStepStart(this, TurnStep.DRAW, s => CanPlace(s.Board));

    public override List<CardStep> OnActivate() =>
        TotalWarCards.ResponseFromDiscardSteps(this, Faction.UNITED_KINGDOM,
            "Discard the top 2 cards of your draw deck to place a Response card from the United Kingdom's discard pile face down on its table",
            s => CanPlace(s.Board), TotalWarCards.DeckDiscardPrice(this, 2));
}
