using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWSubmarinesEnforceBlockade : EWCardLogic
{
    /// <summary>The German Armies beside the North Sea: one VP and two UK discards each. Read by
    /// both the step and <see cref="Targets"/>.</summary>
    private List<UnitState> ScoringUnits
    {
        get
        {
            CountryState northSea = CountryState.ForEnum(Country.NorthSea);
            return FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
                .Where(u => u.Type == UnitType.ARMY && northSea.ConnectedCountryStates.Contains(u.CountryState))
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
                    new ScorePointsChangeEvent(new VPEntry(ScoringUnits.Count, "German Armies adjacent to North Sea"), Faction))
                .OnChosen(_ => _scoringCount = ScoringUnits.Count)),

            new ResultStep(this, Choose.Fixed(() => new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, _scoringCount * 2)))
            .RequiringPreviousStep()
        };
    }
}
