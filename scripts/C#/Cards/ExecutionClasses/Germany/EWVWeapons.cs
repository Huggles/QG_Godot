using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWVWeapons : EWCardLogic
{
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                // Check if German Army in Western Europe
                var germanArmyInWE = FactionState.ForEnum(Faction).ActiveUnitIds.ToUnitStates()
                    .Any(u => u.Type == UnitType.ARMY && u.CountryState.Country == Country.WesternEurope);
                
                if (germanArmyInWE)
                {
                    // Score 3 VP
                    IVictoryStepHandler vpHandler = GameFlow.Instance.vpStepHandler;
                    await vpHandler.ScorePoints(new VPEntry(3, "3 VP for German Army in Western Europe."));
                    
                    // UK discards 1 card
                    ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, 1));
                    discardEvent.IsTrigger = true;
                    return discardEvent;
                }
                
                return null;
            })
        }; 
    }
}