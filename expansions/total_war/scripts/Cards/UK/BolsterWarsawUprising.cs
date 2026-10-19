using System.Collections.Generic;

/// <summary>"Use at the beginning of your Victory step. Discard 2 cards from your hand to recruit an Army in Eastern Europe."</summary>
public partial class BolsterWarsawUprising : BolsterCardLogic
{
    private static readonly List<Country> EasternEurope = [Country.EasternEurope];

    private bool CanRecruit(BoardState board) => TotalWarCards.DeployTargets(board, Faction, DeployType.RECRUIT, UnitType.ARMY, EasternEurope).Count > 0;

    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.RECRUIT, UnitType.ARMY, EasternEurope));

    protected override List<Condition> CardTriggers() => TotalWarCards.AtStepStart(this, TurnStep.VICTORY_POINT,
        s => CanRecruit(s.Board) && TotalWarCards.OtherHandCards(s.Board, this) >= 2);

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.HandDiscardStep(this, 2, "Discard 2 cards to recruit an Army in Eastern Europe", s => CanRecruit(s.Board)),
        TotalWarCards.DeployStep(this, DeployType.RECRUIT, UnitType.ARMY, EasternEurope, "Recruit an Army in Eastern Europe").RequiringPreviousStep()
    };
}
