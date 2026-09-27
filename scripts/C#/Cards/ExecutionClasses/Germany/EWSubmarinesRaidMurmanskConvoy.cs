using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWSubmarinesRaidMurmanskConvoy : EWCardLogic
{
    /// <summary>The German pieces in or beside Scandinavia: one VP and two Soviet discards each.
    /// Read by both the step and <see cref="Targets"/>.</summary>
    private List<UnitState> ScoringUnits
    {
        get
        {
            CountryState scandinavia = CountryState.ForEnum(Country.Scandinavia);
            return FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
                .Where(u => u.CountryState.Country == Country.Scandinavia
                         || scandinavia.ConnectedCountryStates.Contains(u.CountryState))
                .ToList();
        }
    }

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
                    new ScorePointsChangeEvent(new VPEntry(ScoringUnits.Count, "German units in or adjacent to Scandinavia"), Faction))
                .OnChosen(_ => _scoringCount = ScoringUnits.Count)),

            new ResultStep(this, Choose.Fixed(() => new ForceDiscardCardsChangeEvent(Faction, Faction.SOVIET, _scoringCount * 2)))
            .RequiringPreviousStep()
        };
    }
}
