using System.Collections.Generic;

public partial class EventStrategicPlanning : EventCardLogic
{
    // No Targets() override: the choice is among deck cards, which have no place on the board.

    private List<int> DeckChoices(BoardState board) => TotalWarCards.DeckChoices(board, Faction);

    /// <summary>
    /// A chain: each step needs the one before, so only the first take decides whether the card can be
    /// played. Declining the second card is a successful step (Nothing), so the discard and shuffle still follow.
    /// </summary>
    public override List<CardStep> OnActivate() => new()
    {
        new RequirementStep(this, async () => {
            var pick = await new InputRequest.SelectCardRequestHandler(Faction, DeckChoices(BoardState.Live), "Take a card from your draw deck").BroadCast();
            if (pick.ResponseCardIds.Count == 0) throw new StepSkippedException();
            return new RecycleCardChangeEvent(Faction, Faction, pick.ResponseCardIds[0], RecycleDestination.Hand);
        })
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => DeckChoices(s.Board).Count > 0), this))
        .WithGuidance("Take a card of its choice from its draw deck"),

        new RequirementStep(this, async () => {
            List<int> choices = DeckChoices(BoardState.Live);
            if (choices.Count == 0) return CardStepResult.Nothing;
            var pick = await new InputRequest.SelectCardRequestHandler(Faction, choices, "Take a second card from your draw deck, or pass", required: false).BroadCast();
            return pick.ResponseCardIds.Count > 0
                ? new RecycleCardChangeEvent(Faction, Faction, pick.ResponseCardIds[0], RecycleDestination.Hand)
                : CardStepResult.Nothing;
        })
        .RequiringPreviousStep(),

        new RequirementStep(this, Choose.Fixed(_ => new ForceDiscardHandCardsChangeEvent(Faction, Faction, 1)))
            .RequiringPreviousStep()
            .WithGuidance("Discard a card from its hand"),

        TotalWarCards.ShuffleStep(this)
            .RequiringPreviousStep()
    };
}
