using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWSubmarinesRaidMurmanskConvoy : EWCardLogic
{
    public override List<CardStep> InitializePlayCardSteps()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                // Find Scandinavia
                CountryState scandinavia = CountryState.ForEnum(Country.Scandinavia);
                
                // Count German units in or adjacent to Scandinavia
                var germanUnits = FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
                    .Where(u => u.CountryState.Country == Country.Scandinavia || 
                               scandinavia.NeighborCountryStates.Contains(u.CountryState))
                    .ToList();
                
                int count = germanUnits.Count;
                
                IVictoryStepHandler vpHandler = GameSession.Current.GameFlow.vpStepHandler;
                await vpHandler.ScorePoints(new VPEntry(count, $"{count} VP for German units in or adjacent to Scandinavia."));

                DiscardCardsChangeEvent discardEvent = BuildChangeEvent(new DiscardCardsChangeEvent(Faction, Faction.SOVIET, count * 2));
                discardEvent.IsTrigger = true;
                return discardEvent;
            })
        }; 
    }
}