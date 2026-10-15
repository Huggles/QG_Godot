using System.Collections.Generic;

public partial class ResponseKantaiKessen : ResponseCardLogic
{
    public override bool IsFreePlayStepActivation => true;

    private List<BattleTarget> BattleTargets(GameSituation situation) =>
        TotalWarCards.BattleTargetsOn(situation.Board, Faction, CountryType.SEA);

    public override TargetSet Targets() => TargetSet.FromBattleTargets(BattleTargets(GameSituation.Live));

    protected override List<Condition> CardTriggers() => TotalWarCards.AtPlayStepStart(this);

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.BattleStep(this, BattleTargets, "Battle a sea space")
    };
}
