using System.Collections.Generic;
using System.Linq;

public partial class EventPolskiePanstwoPodziemne : EventCardLogic
{
    private static List<int> AxisArmies(BoardState board) =>
        TotalWarCards.TeamUnitsIn(board, FactionTeam.AXIS, UnitType.ARMY, [Country.EasternEurope])
            .Where(unit => !board.ImmuneForTurn(unit)).Select(unit => unit.Id).ToList();

    public override TargetSet Targets() => TargetSet.Units(AxisArmies(BoardState.Live));

    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.UnitFrom(c => AxisArmies(c.Board),
                (unitId, c) => new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE, c.Board)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => AxisArmies(s.Board).Count > 0), this))
            .WithGuidance("Eliminate an Axis Army in Eastern Europe")
    };
}
