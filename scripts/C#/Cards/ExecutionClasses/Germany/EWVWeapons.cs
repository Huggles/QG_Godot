using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWVWeapons : EWCardLogic
{
    /// <summary>The German Armies in Western Europe. The card pays a flat 3 VP if there is at least
    /// one, so the preview shows what is unlocking it — and Western Europe itself, so an empty board
    /// still says where to look.</summary>
    private List<UnitState> ScoringUnits(BoardState board) =>
        board.ActiveUnits(Faction)
            .Where(u => u.Type == UnitType.ARMY && board.CountryOf(u) == (int)Country.WesternEurope)
            .ToList();

    public override TargetSet Targets() =>
        TargetSet.Countries(new List<Country> { Country.WesternEurope })
            .Plus(TargetSet.Units(ScoringUnits(BoardState.Live)));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new ResultStep(this, Choose.Fixed(c => ScoringUnits(c.Board).Count == 0
                    ? null
                    : new ScorePointsChangeEvent(new VPEntry(3, "German Army in Western Europe"), Faction))),

            new ResultStep(this, Choose.Fixed(c => ScoredBy(c.Previous) == 0 ? null
                : new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, 1)))
            .RequiringPreviousStep()
        };
    }
}
