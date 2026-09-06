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

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                int count = ScoringUnits.Count;
                
                await CardPlayPool.DoChangeEvent(new ScorePointsChangeEvent(new VPEntry(count, "German units in or adjacent to Scandinavia"), Faction));

                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction.SOVIET, count * 2));
                discardEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(discardEvent);
            })
        }; 
    }
}