using System.Collections.Generic;

/// <summary>
/// Reallocate Resources from the discard pile instead of the draw deck, as a second action beside the hand:
/// the same cost (Victory Gardens included) and the same Build/Battle cards, through the core action's
/// public helpers. Shaped like base StatusGuards: a discarded card goes back to hand, which re-arms its
/// used steps, and is played from there.
/// </summary>
public partial class StatusWarBonds : StatusCardLogic
{
    /// <summary>The discard-pile card picked by the second step, recycled and played by the last two.</summary>
    private int _taken = -1;

    private int Cost(BoardState board) => MutatorReallocateResources.DiscardCostFor(Faction, board);

    private List<int> TakeOptions(GameSituation situation) =>
        MutatorReallocateResources.TakeOptions(situation.Board.ForFaction(Faction).Discarded, situation);

    public override TargetSet Targets() => TargetSet.Cards(TakeOptions(GameSituation.Live));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.IsPlayCardStep(), this),
        Condition.Build(new Condition.IsFactionTurn(Faction), this),
        Condition.Build(new Condition.CustomCondition(s => s.Board.ForFaction(Faction).Hand.Count >= Cost(s.Board)), this),
        Condition.Build(new Condition.CustomCondition(s => TakeOptions(s).Count > 0), this)
    };

    /// <summary>The card is picked before the cost is paid, so the cards discarded to pay it are not among the choices.</summary>
    public override List<CardStep> OnActivate() => new()
    {
        new RequirementStep(this, Choose.Fixed(_ => new SpendPlayActionChangeEvent(Faction)))
            .WithGuidance("Discard cards from its hand to take a Build or Battle card from its discard pile and play it"),

        new RequirementStep(this, async () => {
            var pick = await new InputRequest.SelectCardRequestHandler(Faction, TakeOptions(GameSituation.Live), "Take a card from your discard pile and play it").BroadCast();
            if (pick.ResponseCardIds.Count == 0) throw new StepSkippedException();
            _taken = pick.ResponseCardIds[0];
            return CardStepResult.Nothing;
        })
        .RequiringPreviousStep(),

        new RequirementStep(this, Choose.Fixed(c => new ForceDiscardHandCardsChangeEvent(Faction, Faction, Cost(c.Board))))
            .RequiringPreviousStep(),

        new RequirementStep(this, Choose.Fixed(_ => new RecycleCardChangeEvent(Faction, Faction, _taken, RecycleDestination.Hand)))
            .RequiringPreviousStep(),

        new PlayCardStep(this, PlayChoice.Fixed(_ => _taken))
            .RequiringPreviousStep()
    };
}
