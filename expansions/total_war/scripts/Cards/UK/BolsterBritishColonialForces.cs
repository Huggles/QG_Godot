using System.Collections.Generic;

/// <summary>"Use at the beginning of your Victory step. Discard 2 cards from your hand to recruit an Army in North Africa, Middle East, Southeast Asia, or Indonesia."</summary>
public partial class BolsterBritishColonialForces : BolsterCardLogic
{
    private static readonly List<Country> Colonies = [Country.NorthAfrica, Country.MiddleEast, Country.SouthEastAsia, Country.Indonesia];

    private bool CanRecruit(BoardState board) => TotalWarCards.DeployTargets(board, Faction, DeployType.RECRUIT, UnitType.ARMY, Colonies).Count > 0;

    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.RECRUIT, UnitType.ARMY, Colonies));

    protected override List<Condition> CardTriggers() => TotalWarCards.AtStepStart(this, TurnStep.VICTORY_POINT,
        s => CanRecruit(s.Board) && TotalWarCards.OtherHandCards(s.Board, this) >= 2);

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.HandDiscardStep(this, 2, "Discard 2 cards to recruit an Army in the colonies", s => CanRecruit(s.Board)),
        TotalWarCards.DeployStep(this, DeployType.RECRUIT, UnitType.ARMY, Colonies, "Recruit an Army in North Africa, the Middle East, South East Asia, or Indonesia")
            .RequiringPreviousStep()
    };
}
