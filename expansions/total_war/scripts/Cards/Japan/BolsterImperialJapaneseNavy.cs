using System.Collections.Generic;

/// <summary>"Use when you build a Navy. Discard a Response card from your hand to build an additional Navy."</summary>
public partial class BolsterImperialJapaneseNavy : BolsterCardLogic
{
    private List<CountryState> NavyTargets(GameSituation situation) =>
        TotalWarCards.DeployTargets(situation.Board, Faction, DeployType.BUILD, UnitType.NAVY);

    public override TargetSet Targets() => TargetSet.Countries(NavyTargets(GameSituation.Live));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.HasDeployedNavy(Faction).Immediately(), this),
        Condition.Build(new Condition.CustomCondition(s => NavyTargets(s).Count > 0), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.TypedDiscardStep(this, CardType.RESPONSE, "Discard a Response card to build an additional Navy", s => NavyTargets(s).Count > 0),
        TotalWarCards.DeployStep(this, DeployType.BUILD, UnitType.NAVY, null, "Build an additional Navy").RequiringPreviousStep()
    };
}
