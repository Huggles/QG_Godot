using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class EventStrategicPlanning : EventCardLogic
{
    // No Targets() override: the choice is among deck cards, which have no place on the board.

    private const int MaxCards = 2;

    /// <summary>The deck cards picked by the first step; the second step recycles the other one, if any.</summary>
    private List<int> _taken = new();

    private List<int> DeckChoices(BoardState board) => TotalWarCards.DeckChoices(board, Faction, perName: MaxCards);

    /// <summary>
    /// A chain: each step needs the one before, so only the take decides whether the card can be played.
    /// Both cards are picked on one screen; a step yields one event, so the second card's recycle is its own step.
    /// </summary>
    public override List<CardStep> OnActivate() => new()
    {
        new RequirementStep(this, async () => {
            List<int> choices = DeckChoices(BoardState.Live);
            var pick = await new InputRequest.SelectCardsRequestHandler(Faction, choices, "Take 1 or 2 cards from your draw deck", 1, MaxCards).BroadCast();
            _taken = pick.ResponseCardIds.Distinct().Where(choices.Contains).Take(MaxCards).ToList();
            if (_taken.Count == 0) throw new StepSkippedException();
            return new RecycleCardChangeEvent(Faction, Faction, _taken[0], RecycleDestination.Hand);
        })
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => DeckChoices(s.Board).Count > 0), this))
        .WithGuidance("Take 1 or 2 cards of its choice from its draw deck"),

        new RequirementStep(this, () => Task.FromResult(_taken.Count > 1
            ? new RecycleCardChangeEvent(Faction, Faction, _taken[1], RecycleDestination.Hand)
            : CardStepResult.Nothing))
        .RequiringPreviousStep(),

        new RequirementStep(this, Choose.Fixed(_ => new ForceDiscardHandCardsChangeEvent(Faction, Faction, 1)))
            .RequiringPreviousStep()
            .WithGuidance("Discard a card from its hand"),

        TotalWarCards.ShuffleStep(this)
            .RequiringPreviousStep()
    };
}
