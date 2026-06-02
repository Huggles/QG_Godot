using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWSubmarinesSupportPacificIslands : EWCardLogic
{
    public override List<CardStep> InitializePlayCardSteps()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                // Find East Pacific
                CountryState eastPacific = CountryState.ForEnum(Country.EastPacific);
                
                // Count Japanese Navies in or adjacent to East Pacific
                var japaneseNavies = FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
                    .Where(u => u.Type == UnitType.NAVY && 
                               (u.CountryState.Country == Country.EastPacific || 
                               eastPacific.NeighborCountryStates.Contains(u.CountryState)))
                    .ToList();
                
                int count = japaneseNavies.Count;
                
                if (count > 0)
                {
                    // Score 2 VP per navy
                    IVictoryStepHandler vpHandler = GameFlow.Instance.vpStepHandler;
                    await vpHandler.ScorePoints(new VPEntry(count * 2, $"{count * 2} VP for Japanese Navies in or adjacent to East Pacific."));
                    
                    // US discards 2 cards per navy
                    ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_STATES, count * 2));
                    discardEvent.IsTrigger = true;
                    return discardEvent;
                }
                
                return null;
            })
        }; 
    }
}