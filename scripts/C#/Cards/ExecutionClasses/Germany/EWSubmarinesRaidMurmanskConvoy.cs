using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWSubmarinesRaidMurmanskConvoy : EWCardLogic
{
    /// <summary>The German pieces in or beside Scandinavia: one VP and two Soviet discards each.
    /// Read by both the step and <see cref="Targets"/>.</summary>
    private List<UnitState> ScoringUnits(BoardState board)
    {
        CountryState scandinavia = CountryState.ForEnum(Country.Scandinavia);
        return board.ActiveUnits(Faction)
            .Where(u => board.CountryOf(u) == (int)Country.Scandinavia
                     || scandinavia.ConnectedCountryIds.Contains(board.CountryOf(u)))
            .ToList();
    }

    public override TargetSet Targets() => TargetSet.Units(ScoringUnits(BoardState.Live));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new ResultStep(this, Choose.Fixed(c =>
                    new ScorePointsChangeEvent(VPEntry.ForUnits(ScoringUnits(c.Board), c.Board, 1, "German units in or adjacent to Scandinavia"), Faction))),

            new ResultStep(this, Choose.Fixed(c => new ForceDiscardCardsChangeEvent(Faction, Faction.SOVIET, ScoredBy(c.Previous) * 2)))
            .RequiringPreviousStep()
        };
    }
}
