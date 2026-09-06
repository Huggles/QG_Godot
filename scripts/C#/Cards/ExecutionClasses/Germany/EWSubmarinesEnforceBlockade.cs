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
            new CardStep(this, async() => {
                int count = ScoringUnits.Count;

                await CardPlayPool.DoChangeEvent(new ScorePointsChangeEvent(new VPEntry(count, "German Armies adjacent to North Sea"), Faction));
                
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, count * 2));
                discardEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(discardEvent);
            })
        }; 
    }
}