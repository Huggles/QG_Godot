using System.Collections.Generic;

public partial class ResponseImperialManchukuoArmy : ResponseCardLogic
{
    private static readonly List<Country> china = [Country.China];

    public override bool IsFreePlayStepActivation => true;

    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.RECRUIT, UnitType.ARMY, china));

    protected override List<Condition> CardTriggers() => TotalWarCards.AtPlayStepStart(this);

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeployStep(this, DeployType.RECRUIT, UnitType.ARMY, china, "Recruit an Army in China")
    };
}
