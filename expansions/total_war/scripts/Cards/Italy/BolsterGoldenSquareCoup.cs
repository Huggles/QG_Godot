using System.Collections.Generic;

/// <summary>"Use at the beginning of your Victory step. Recruit an Army in the Middle East."</summary>
public partial class BolsterGoldenSquareCoup : BolsterCardLogic
{
    private static readonly List<Country> MiddleEast = [Country.MiddleEast];

    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.RECRUIT, UnitType.ARMY, MiddleEast));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.AtStepStart(TurnStep.VICTORY_POINT), this),
        Condition.Build(new Condition.CustomCondition(s =>
            TotalWarCards.DeployTargets(s.Board, Faction, DeployType.RECRUIT, UnitType.ARMY, MiddleEast).Count > 0), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeployStep(this, DeployType.RECRUIT, UnitType.ARMY, MiddleEast, "Recruit an Army in the Middle East")
    };
}
