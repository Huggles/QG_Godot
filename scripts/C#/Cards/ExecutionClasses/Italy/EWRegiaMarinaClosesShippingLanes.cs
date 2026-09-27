using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWRegiaMarinaClosesShippingLanes : EWCardLogic
{
    /// <summary>Every Italian Navy on the board: one VP and one UK discard each. Read by both the
    /// step and <see cref="Targets"/>.</summary>
    private List<UnitState> ScoringUnits =>
        FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
            .Where(u => u.Type == UnitType.NAVY)
            .ToList();

    public override TargetSet Targets() => TargetSet.Units(ScoringUnits);

    /// <summary>
    /// Captured by the scoring step and read by the discard step, rather than recomputed. The two
    /// are separate steps now, and a reaction played in the scoring step's after-reaction window can
    /// move the board between them -- recomputing would make the discard disagree with the VP that
    /// was actually awarded.
    /// </summary>
    private int _scoringCount;

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new ResultStep(this, Choose.Fixed(() =>
                new ScorePointsChangeEvent(new VPEntry(ScoringUnits.Count, "Italian Navies on the board"), Faction))
            .OnChosen(_ => _scoringCount = ScoringUnits.Count)),

            new ResultStep(this, Choose.Fixed(() =>
                new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, _scoringCount)))
            .RequiringPreviousStep()
        };
    }
}
