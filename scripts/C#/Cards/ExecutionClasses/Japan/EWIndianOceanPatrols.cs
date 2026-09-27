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

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new ResultStep(this, Choose.Fixed(() => {
                int count = ScoringUnits.Count;
                return count == 0 ? null
                    : new ScorePointsChangeEvent(new VPEntry(count * 2, "Japanese Navies in or adjacent to Bay of Bengal"), Faction);
            })),

            new ResultStep(this, Choose.Fixed(previous => ScoredBy(previous) == 0 ? null
                : new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, ScoredBy(previous))))
            .RequiringPreviousStep()
        };
    }
}
