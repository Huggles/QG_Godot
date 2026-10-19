using System.Collections.Generic;

/// <summary>"Use at the beginning of your Victory step. Recruit an Army in Africa."</summary>
public partial class BolsterSomaliland : BolsterCardLogic
{
    private static readonly List<Country> Africa = [Country.Africa];

    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.RECRUIT, UnitType.ARMY, Africa));

    protected override List<Condition> CardTriggers() => TotalWarCards.AtStepStart(this, TurnStep.VICTORY_POINT,
        s => TotalWarCards.DeployTargets(s.Board, Faction, DeployType.RECRUIT, UnitType.ARMY, Africa).Count > 0);

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeployStep(this, DeployType.RECRUIT, UnitType.ARMY, Africa, "Recruit an Army in Africa")
    };
}
