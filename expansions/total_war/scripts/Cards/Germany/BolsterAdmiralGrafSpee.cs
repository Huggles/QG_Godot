using System.Collections.Generic;

/// <summary>
/// "Use when you build a Navy. Discard the top 1 card of your draw deck to battle the North Sea, North
/// Atlantic, or South Atlantic." Offered from hand in the after-reaction window of Germany's own Navy build
/// and discarded as it is used (UseBolsterChangeEvent); the cost is its first step, the battle its second.
/// </summary>
public partial class BolsterAdmiralGrafSpee : BolsterCardLogic
{
    private static readonly List<Country> Oceans = [Country.NorthSea, Country.NorthAtlantic, Country.SouthAtlantic];

    private List<BattleTarget> BattleTargets(GameSituation situation) => TotalWarCards.BattleTargetsIn(situation.Board, Faction, Oceans);

    public override TargetSet Targets() => TargetSet.FromBattleTargets(BattleTargets(GameSituation.Live));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.HasDeployedNavy(Faction).Immediately(), this),
        Condition.Build(new Condition.CustomCondition(s => BattleTargets(s).Count > 0), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        new RequirementStep(this, Choose.Fixed(_ => new ForceDiscardCardsChangeEvent(Faction, Faction, 1)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => BattleTargets(s).Count > 0), this))
            .WithGuidance("Discard the top card of your draw deck to battle the North Sea, North Atlantic or South Atlantic"),

        TotalWarCards.BattleStep(this, BattleTargets, "Battle the North Sea, North Atlantic or South Atlantic")
            .RequiringPreviousStep()
    };
}
