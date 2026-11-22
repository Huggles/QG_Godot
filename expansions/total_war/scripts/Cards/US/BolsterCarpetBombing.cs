using System.Collections.Generic;

/// <summary>"Use when you battle. Discard an Economic Warfare card from your hand to battle the same space again."</summary>
public partial class BolsterCarpetBombing : BolsterCardLogic
{
    private List<BattleTarget> BattleTargets(GameSituation situation) =>
        TriggerContextAs<BattleCountryChangeEvent>(situation)?.CountryState is { } battled
            ? TotalWarCards.BattleTargetsIn(situation.Board, Faction, [battled.Country])
            : new();

    public override TargetSet Targets() => TargetSet.FromBattleTargets(BattleTargets(GameSituation.Live));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.FactionBattled(Faction).Immediately(), this),
        Condition.Build(new Condition.CustomCondition(s => BattleTargets(s).Count > 0), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.BattleStep(this, BattleTargets, "Discard an Economic Warfare card to battle the same space again")
            .WithPrice(TotalWarCards.TypedDiscardPrice(this, CardType.ECONOMIC_WARFARE, "Discard an Economic Warfare card"))
    };
}
