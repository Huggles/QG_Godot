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
    private List<UnitState> ScoringUnits =>
        FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
            .Where(u => u.Type == UnitType.ARMY && u.CountryState.Country == Country.WesternEurope)
            .ToList();

    public override TargetSet Targets() =>
        TargetSet.Countries(new List<Country> { Country.WesternEurope })
            .Plus(TargetSet.Units(ScoringUnits));

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
                    new ScorePointsChangeEvent(new VPEntry(3, "German Army in Western Europe"), Faction));
            }),

            new ResultStep(this, () => _scoringCount == 0
                ? Task.FromResult(CardStepResult.Nothing)
                : Task.FromResult<CardStepResult>(
                    new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, 1)))
            .RequiringPreviousStep()
        };
    }
}
