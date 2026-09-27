using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWSubmarinesSupportPacificIslands : EWCardLogic
{
    /// <summary>The Japanese Navies in or beside the East Pacific: two VP and two discards each. Read by
    /// both the step and <see cref="Targets"/>, so hovering shows exactly what this card is worth.</summary>
    private List<UnitState> ScoringUnits
    {
        get
        {
            CountryState eastPacific = CountryState.ForEnum(Country.EastPacific);
            return FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
                .Where(u => u.Type == UnitType.NAVY
                         && (u.CountryState.Country == Country.EastPacific
                             || eastPacific.ConnectedCountryStates.Contains(u.CountryState)))
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
            new ResultStep(this, Choose.Fixed(() => {
                int count = ScoringUnits.Count;
                return count == 0 ? null
                    : new ScorePointsChangeEvent(new VPEntry(count * 2, "Japanese Navies in or adjacent to East Pacific"), Faction);
            })
            .OnChosen(_ => _scoringCount = ScoringUnits.Count)),

            new ResultStep(this, Choose.Fixed(() => _scoringCount == 0 ? null
                : new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_STATES, _scoringCount * 2)))
            .RequiringPreviousStep()
        };
    }
}
