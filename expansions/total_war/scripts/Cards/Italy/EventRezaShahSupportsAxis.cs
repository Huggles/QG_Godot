using System.Collections.Generic;
using System.Linq;

public partial class EventRezaShahSupportsAxis : EventCardLogic
{
    /// <summary>Base Allied Armies only: the card names neither France nor China.</summary>
    private static List<int> AlliedArmies(BoardState board) =>
        TotalWarCards.TeamUnitsIn(board, FactionTeam.ALLIES, UnitType.ARMY, [Country.MiddleEast])
            .Where(unit => !board.ImmuneForTurn(unit)).Select(unit => unit.Id).ToList();

    public override TargetSet Targets() => TargetSet.Units(AlliedArmies(BoardState.Live));

    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, Choose.UnitFrom(c => AlliedArmies(c.Board),
                (unitId, c) => new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE, c.Board)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => AlliedArmies(s.Board).Count > 0), this))
            .WithGuidance("Eliminate an Allied Army in the Middle East")
    };
}
