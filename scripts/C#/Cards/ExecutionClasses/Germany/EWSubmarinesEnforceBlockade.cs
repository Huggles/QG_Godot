using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWSubmarinesEnforceBlockade : EWCardLogic
{
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                // Find North Sea country
                CountryState northSea = CountryState.ForEnum(Country.NorthSea);
                
                // Count German Armies adjacent to North Sea
                var germanArmies = FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
                    .Where(u => u.Type == UnitType.ARMY && northSea.ConnectedCountryStates.Contains(u.CountryState))
                    .ToList();
                
                int count = germanArmies.Count;

                await CardPlayPool.DoChangeEvent(new ScorePointsChangeEvent(new VPEntry(count, "German Armies adjacent to North Sea"), Faction));
                
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, count * 2));
                discardEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(discardEvent);
            })
        }; 
    }
}