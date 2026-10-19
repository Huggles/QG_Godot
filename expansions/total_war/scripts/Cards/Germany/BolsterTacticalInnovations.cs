using System.Collections.Generic;
using System.Linq;

/// <summary>
/// "Use at the beginning of your Play step. Discard your Status card from the table to place a Status card
/// from your hand on the table." The new Status card is placed with PlayCardChangeEvent.CountsAsPlay false,
/// so the turn's play is still to come.
/// </summary>
public partial class BolsterTacticalInnovations : BolsterCardLogic
{
    private List<int> OnTable(BoardState board) => board.ForFaction(Faction).Status.ToList();
    private List<int> InHand(BoardState board) => TotalWarCards.InHand(board, Faction, CardType.STATUS);

    public override TargetSet Targets() => TargetSet.Cards(OnTable(BoardState.Live));

    protected override List<Condition> CardTriggers() => TotalWarCards.AtPlayStepStart(this)
        .Append(Condition.Build(new Condition.CustomCondition(s => OnTable(s.Board).Count > 0 && InHand(s.Board).Count > 0), this))
        .ToList();

    public override List<CardStep> OnActivate() => new()
    {
        new RequirementStep(this, async () => {
            int chosen = (await new InputRequest.SelectCardsRequestHandler(Faction, OnTable(BoardState.Live),
                "Discard one of your Status cards from the table", 1, 1).BroadCast()).ResponseCardIds.FirstOrDefault(-1);
            if (chosen < 0) throw new StepSkippedException();
            return new DiscardHandCardsChangeEvent(Faction, Faction, new List<int> { chosen });
        })
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => OnTable(s.Board).Count > 0 && InHand(s.Board).Count > 0), this))
        .WithGuidance("Discard one of your Status cards from the table"),

        new RequirementStep(this, async () => {
            int chosen = (await new InputRequest.SelectCardsRequestHandler(Faction, InHand(BoardState.Live),
                "Place a Status card from your hand on the table", 1, 1).BroadCast()).ResponseCardIds.FirstOrDefault(-1);
            if (chosen < 0) throw new StepSkippedException();
            return new PlayCardChangeEvent(chosen) { CountsAsPlay = false };
        })
        .RequiringPreviousStep()
        .WithGuidance("Place a Status card from your hand on the table")
    };
}
