using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWSubmarinesRaidMurmanskConvoy : EWCardLogic
{
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                // Find Scandinavia
                CountryState scandinavia = CountryState.ForEnum(Country.Scandinavia);
                
                // Count German units in or adjacent to Scandinavia
                var germanUnits = FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
                    .Where(u => u.CountryState.Country == Country.Scandinavia || 
                               scandinavia.ConnectedCountryStates.Contains(u.CountryState))
                    .ToList();
                
                int count = germanUnits.Count;
                
                await CardPlayPool.DoChangeEvent(new ScorePointsChangeEvent(new VPEntry(count, $"{count} VP for German units in or adjacent to Scandinavia."), Faction));

                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction.SOVIET, count * 2));
                discardEvent.IsTrigger = true;
                return discardEvent;
            })
        }; 
    }
}