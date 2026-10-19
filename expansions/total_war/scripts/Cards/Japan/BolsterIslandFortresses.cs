using System.Collections.Generic;

/// <summary>"Use at the beginning of your Victory step. Discard a Response card from your hand to recruit an Army in Indonesia, New Guinea, Iwo Jima, or the Philippines."</summary>
public partial class BolsterIslandFortresses : BolsterCardLogic
{
    private static readonly List<Country> Islands = [Country.Indonesia, Country.NewGuinea, Country.IwoJima, Country.Philippines];

    private bool CanRecruit(BoardState board) => TotalWarCards.DeployTargets(board, Faction, DeployType.RECRUIT, UnitType.ARMY, Islands).Count > 0;

    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.RECRUIT, UnitType.ARMY, Islands));

    protected override List<Condition> CardTriggers() => TotalWarCards.AtStepStart(this, TurnStep.VICTORY_POINT,
        s => CanRecruit(s.Board) && TotalWarCards.InHand(s.Board, Faction, CardType.RESPONSE).Count > 0);

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.TypedDiscardStep(this, CardType.RESPONSE, "Discard a Response card to recruit an Army on an island", s => CanRecruit(s.Board)),
        TotalWarCards.DeployStep(this, DeployType.RECRUIT, UnitType.ARMY, Islands, "Recruit an Army in Indonesia, New Guinea, Iwo Jima, or the Philippines")
            .RequiringPreviousStep()
    };
}
