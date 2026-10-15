using System.Collections.Generic;
using System.Linq;

public partial class EventFallWeiss : EventCardLogic
{
    private static readonly List<Country> easternEurope = [Country.EasternEurope];

    private static bool CanRecruit(BoardState board, Faction faction) =>
        TotalWarCards.DeployTargets(board, faction, DeployType.RECRUIT, UnitType.ARMY, easternEurope).Count > 0;

    /// <summary>The hand cards that can be played: Air Power and Bolster cards are used, never played.</summary>
    private List<int> PlayableHand() =>
        DeckState.ForFaction(Faction).HandCardIds.Where(id => CardState.ForId(id).CardData.CardType.IsPlayedFromHand()).ToList();

    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.RECRUIT, UnitType.ARMY, easternEurope));

    public override List<CardStep> OnActivate() => new()
    {
        new RequirementStep(this, Choose.Fixed(_ => new ForceDiscardCardsChangeEvent(Faction, Faction, 1)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => CanRecruit(s.Board, Faction)), this))
            .WithGuidance("Discard the top card of your draw deck to recruit an Army in Eastern Europe"),

        TotalWarCards.DeployStep(this, DeployType.RECRUIT, UnitType.ARMY, easternEurope, "Recruit an Army in Eastern Europe")
            .RequiringPreviousStep(),

        // The whole playable hand, set explicitly as EventLendLease does: the turn's play is already spent,
        // so the handler's default offer would be empty.
        new PlayCardStep(this, async () => {
            var cardResp = await new InputRequest.HandCardPlayRequestHandler(Faction) { TargetCardIds = PlayableHand() }.BroadCast();
            return cardResp.ResponseCardIds.Count > 0 ? CardStepResult.PlayCard(cardResp.ResponseCardIds[0]) : CardStepResult.Nothing;
        })
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(_ => PlayableHand().Count > 0), this))
        .WithGuidance("Play another card from your hand")
    };
}
