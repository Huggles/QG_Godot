using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWRegiaMarinaClosesShippingLanes : EWCardLogic
{
    /// <summary>Every Italian Navy on the board: one VP and one UK discard each. Read by both the
    /// step and <see cref="Targets"/>.</summary>
    private List<UnitState> ScoringUnits(BoardState board) =>
        board.ActiveUnits(Faction)
            .Where(u => u.Type == UnitType.NAVY)
            .ToList();

    public override TargetSet Targets() => TargetSet.Units(ScoringUnits(BoardState.Live));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new ResultStep(this, Choose.Fixed(c =>
                new ScorePointsChangeEvent(new VPEntry(ScoringUnits(c.Board).Count, "Italian Navies on the board"), Faction))),

            new ResultStep(this, Choose.Fixed(c =>
                new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, ScoredBy(c.Previous))))
            .RequiringPreviousStep()
        };
    }
}
