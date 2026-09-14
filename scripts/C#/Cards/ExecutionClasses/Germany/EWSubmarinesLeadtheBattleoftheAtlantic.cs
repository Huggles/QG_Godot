using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWSubmarinesLeadtheBattleoftheAtlantic : EWCardLogic
{
    /// <summary>Every German Navy on the board: one VP and two UK discards each. Read by both the
    /// step and <see cref="Targets"/>, so hovering shows exactly what this card is worth.</summary>
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
            new ResultStep(this, () => {
                _scoringCount = ScoringUnits.Count;
                return Task.FromResult<CardStepResult>(
                    new ScorePointsChangeEvent(new VPEntry(_scoringCount, "German Navies on the board"), Faction));
            }),

            new ResultStep(this, () => Task.FromResult<CardStepResult>(
                new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, _scoringCount * 2)))
            .RequiringPreviousStep()
        };
    }
}
