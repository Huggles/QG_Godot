using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWSubmarinesLeadtheBattleoftheAtlantic : EWCardLogic
{
    /// <summary>Every German Navy on the board: one VP and two UK discards each. Read by both the
    /// step and <see cref="Targets"/>, so hovering shows exactly what this card is worth.</summary>
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
                    new ScorePointsChangeEvent(VPEntry.ForUnits(ScoringUnits(c.Board), c.Board, 1, "German Navies on the board"), Faction))),

            new ResultStep(this, Choose.Fixed(c => new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, ScoredBy(c.Previous) * 2)))
            .RequiringPreviousStep()
        };
    }
}
