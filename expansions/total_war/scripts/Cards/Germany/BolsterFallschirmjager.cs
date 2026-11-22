using System.Collections.Generic;
using System.Linq;

/// <summary>"Use at the beginning of your Play step. Discard the top 1 card of your draw deck to battle a land space adjacent to your Air Force."</summary>
public partial class BolsterFallschirmjager : BolsterCardLogic
{
    private List<BattleTarget> BattleTargets(GameSituation situation) => situation.Board.ActiveAirForceIds(Faction)
        .SelectMany(airForce => situation.Board.AdjacentBattleTargets(Faction, CountryType.LAND, situation.Board.CountryStateOf(UnitState.ForId(airForce))))
        .Distinct().ToList();

    public override TargetSet Targets() => TargetSet.FromBattleTargets(BattleTargets(GameSituation.Live));

    protected override List<Condition> CardTriggers() => TotalWarCards.AtPlayStepStart(this)
        .Append(Condition.Build(new Condition.CustomCondition(s => BattleTargets(s).Count > 0), this))
        .ToList();

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.BattleStep(this, BattleTargets, "Discard the top card of your draw deck to battle a land space next to your Air Force")
            .WithPrice(TotalWarCards.DeckDiscardPrice(this, 1))
    };
}
