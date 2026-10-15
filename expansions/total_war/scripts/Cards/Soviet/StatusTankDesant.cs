using System.Collections.Generic;

public partial class StatusTankDesant : StatusCardLogic
{
    /// <summary>What can be battled on land next to the Army just built.</summary>
    private List<BattleTarget> BattleTargets(GameSituation situation) =>
        TriggerContextAs<DeployUnitChangeEvent>(situation)?.CountryState is { } builtIn
            ? situation.Board.AdjacentBattleTargets(Faction, CountryType.LAND, builtIn)
            : new();

    private bool CanPay(BoardState board) =>
        TotalWarCards.FirstInHand(board, Faction, CardType.BUILD_ARMY) >= 0 && TotalWarCards.FirstInHand(board, Faction, CardType.LAND_BATTLE) >= 0;

    public override TargetSet Targets() => TargetSet.FromBattleTargets(BattleTargets(GameSituation.Live));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.HasDeployedArmy(Faction).Immediately(), this),
        Condition.Build(new Condition.CustomCondition(s => CanPay(s.Board) && BattleTargets(s).Count > 0), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        new RequirementStep(this, Choose.Fixed(c => new DiscardHandCardsChangeEvent(Faction, Faction, new List<int> {
                TotalWarCards.FirstInHand(c.Board, Faction, CardType.BUILD_ARMY),
                TotalWarCards.FirstInHand(c.Board, Faction, CardType.LAND_BATTLE) })))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => CanPay(s.Board) && BattleTargets(s).Count > 0), this))
            .WithGuidance("Discard a Build Army and a Land Battle card to battle a land space adjacent to the Army just built"),

        new ResultStep(this, Choose.BattleTargetFrom(c => BattleTargets(c.Situation), (target, c) => target.ToAttackChangeEvent(Faction, c.Board)))
            .RequiringPreviousStep()
    };
}
