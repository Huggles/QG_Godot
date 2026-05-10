using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWSubmarinesEnforceBlockade : EWCardLogic
{
    public override List<CardStep> InitializePlayCardSteps()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                // Find North Sea country
                CountryState northSea = CountryState.ForEnum(Country.NorthSea);
                
                // Count German Armies adjacent to North Sea
                var germanArmies = FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
                    .Where(u => u.Type == UnitType.ARMY && northSea.NeighborCountryStates.Contains(u.CountryState))
                    .ToList();
                
                int count = germanArmies.Count;

                IVictoryStepHandler vpHandler = GameSession.Instance.GameFlow.vpStepHandler;
                await vpHandler.ScorePoints(new VPEntry(count, $"{count} VP for German Armies adjacent to North Sea."));
                
                DiscardCardsChangeEvent discardEvent = BuildChangeEvent(new DiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, count * 2));
                discardEvent.IsTrigger = true;
                return discardEvent;
            })
        }; 
    }
}