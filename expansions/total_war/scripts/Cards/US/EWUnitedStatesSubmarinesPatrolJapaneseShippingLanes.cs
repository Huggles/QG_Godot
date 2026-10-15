using System.Collections.Generic;
using System.Linq;

public partial class EWUnitedStatesSubmarinesPatrolJapaneseShippingLanes : EWCardLogic
{
    /// <summary>The US Navies within 2 spaces of Japan; each costs Japan 2 cards.</summary>
    private List<UnitState> Navies(BoardState board) =>
        TotalWarCards.Within(board, board.ActiveUnits(Faction).Where(unit => unit.IsNavy), Country.Japan, 2);

    public override TargetSet Targets() => TargetSet.Units(Navies(BoardState.Live));

    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.Fixed(c => new ForceDiscardCardsChangeEvent(Faction, Faction.JAPAN, 2 * Navies(c.Board).Count)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => Navies(s.Board).Count > 0), this))
            .WithGuidance("Make Japan discard 2 cards for each US Navy within 2 spaces of Japan")
    };
}
