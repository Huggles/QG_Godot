using System.Collections.Generic;

/// <summary>
/// "Use when you play an Economic Warfare card. The United Kingdom must discard the top 2 cards of its draw
/// deck for each German Air Force within 2 spaces of the United Kingdom." Offered in the windows that EW
/// card's own steps open, this turn's Play step only.
/// </summary>
public partial class BolsterTheBlitz : BolsterCardLogic
{
    private int AirForcesInRange(BoardState board) =>
        TotalWarCards.Within(board, UnitState.ForIds(board.ActiveAirForceIds(Faction)), Country.UnitedKingdom, 2).Count;

    public override TargetSet Targets() => TargetSet.Units(TotalWarCards.Within(BoardState.Live,
        UnitState.ForIds(BoardState.Live.ActiveAirForceIds(Faction)), Country.UnitedKingdom, 2));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.FactionPlayedCard(Faction), this),
        Condition.Build(new Condition.IsGameFlowStep(TurnStep.PLAY_CARD), this),
        Condition.Build(new Condition.IsFactionTurn(Faction), this),
        Condition.Build(new Condition.CustomCondition(s =>
            TotalWarCards.EconomicWarfarePlayed(s, Faction) != null && AirForcesInRange(s.Board) > 0), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.Fixed(c => new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, 2 * AirForcesInRange(c.Board))))
            .WithGuidance("Make the United Kingdom discard 2 cards from its deck for each German Air Force within 2 spaces of it")
    };
}
