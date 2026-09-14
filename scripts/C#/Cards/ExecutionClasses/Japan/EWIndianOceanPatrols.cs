using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWIndianOceanPatrols : EWCardLogic
{
    /// <summary>The Japanese Navies in or beside the Bay of Bengal: two VP and two discards each. Read by
    /// both the step and <see cref="Targets"/>, so hovering shows exactly what this card is worth.</summary>
    private List<UnitState> ScoringUnits
    {
        get
        {
            CountryState bayOfBengal = CountryState.ForEnum(Country.BayOfBengal);
            return FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
                .Where(u => u.Type == UnitType.NAVY
                         && (u.CountryState.Country == Country.BayOfBengal
                             || bayOfBengal.ConnectedCountryStates.Contains(u.CountryState)))
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
            new ResultStep(this, () => {
                _scoringCount = ScoringUnits.Count;
                if (_scoringCount == 0) return Task.FromResult(CardStepResult.Nothing);
                return Task.FromResult<CardStepResult>(
                    new ScorePointsChangeEvent(new VPEntry(_scoringCount * 2, "Japanese Navies in or adjacent to Bay of Bengal"), Faction));
            }),

            new ResultStep(this, () => _scoringCount == 0
                ? Task.FromResult(CardStepResult.Nothing)
                : Task.FromResult<CardStepResult>(
                    new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, _scoringCount * 2)))
            .RequiringPreviousStep()
        };
    }
}
