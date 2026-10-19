using System.Collections.Generic;
using System.Linq;

/// <summary>"Use at the beginning of your Draw step. Discard a Build Army card from your hand to place a Response card from your discard pile face down on the table."</summary>
public partial class BolsterFortressRebuilt : BolsterCardLogic
{
    private bool CanPlace(BoardState board) => TotalWarCards.DiscardedResponses(board, Faction, this).Count > 0;

    protected override List<Condition> CardTriggers() => TotalWarCards.AtStepStart(this, TurnStep.DRAW,
        s => CanPlace(s.Board) && TotalWarCards.InHand(s.Board, Faction, CardType.BUILD_ARMY).Count > 0);

    public override List<CardStep> OnActivate() =>
        new List<CardStep> { TotalWarCards.TypedDiscardStep(this, CardType.BUILD_ARMY, "Discard a Build Army card to place a Response card from your discard pile",
                s => CanPlace(s.Board)) }
            .Concat(TotalWarCards.ResponseFromDiscardSteps(this, Faction, "Place a Response card from your discard pile face down on the table")
                .Select(step => step.RequiringPreviousStep()))
            .ToList();
}
