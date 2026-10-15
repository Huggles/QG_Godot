using System.Collections.Generic;

public partial class ResponseThailand : ResponseCardLogic
{
    private static readonly List<Country> southEastAsia = [Country.SouthEastAsia];

    public override bool IsFreePlayStepActivation => true;

    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.RECRUIT, UnitType.ARMY, southEastAsia));

    protected override List<Condition> CardTriggers() => TotalWarCards.AtPlayStepStart(this);

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeployStep(this, DeployType.RECRUIT, UnitType.ARMY, southEastAsia, "Recruit an Army in South East Asia")
    };
}
