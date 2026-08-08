using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWIndianOceanPatrols : EWCardLogic
{
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                // Find Bay of Bengal
                CountryState bayOfBengal = CountryState.ForEnum(Country.BayOfBengal);
                
                // Count Japanese Navies in or adjacent to Bay of Bengal
                var japaneseNavies = FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
                    .Where(u => u.Type == UnitType.NAVY && 
                               (u.CountryState.Country == Country.BayOfBengal || 
                               bayOfBengal.ConnectedCountryStates.Contains(u.CountryState)))
                    .ToList();
                
                int count = japaneseNavies.Count;
                
                if (count > 0)
                {
                    // Score 2 VP per navy through the ChangeEvent pipeline
                    await CardPlayPool.DoChangeEvent(new ScorePointsChangeEvent(
                        new VPEntry(count * 2, "Japanese Navies in or adjacent to Bay of Bengal"),
                        Faction));
                    
                    // UK discards 2 cards per navy
                    ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, count * 2));
                    discardEvent.IsTrigger = true;
                    return discardEvent;
                }
                
                return null;
            })
        }; 
    }
}