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

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new ResultStep(this, Choose.Fixed(() =>
                    new ScorePointsChangeEvent(new VPEntry(ScoringUnits.Count, "German Armies adjacent to North Sea"), Faction))),

            new ResultStep(this, Choose.Fixed(previous => new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, ScoredBy(previous) * 2)))
            .RequiringPreviousStep()
        };
    }
}
