using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EWSubmarinesPreyonUnprotectedShipping : EWCardLogic
{
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                // Check if Allied Navy in North Sea
                CountryState northSea = CountryState.ForEnum(Country.NorthSea);
                bool alliedNavyInNorthSea = northSea.Units.Values.Any(unitId => 
                {
                    UnitState unit = UnitState.ForId(unitId);
                    return unit.Type == UnitType.NAVY && unit.FactionTeam == FactionTeam.ALLIES;
                });
                
                int discardCount = alliedNavyInNorthSea ? 2 : 5;
                
                // UK discards cards
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction.UNITED_KINGDOM, discardCount));
                discardEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(discardEvent);
                return null;
            }),
            new CardStep(this, async() => {               
                // Score 1 VP       
                ScorePointsChangeEvent scorePointsEvent = BuildChangeEvent(new ScorePointsChangeEvent(new VPEntry(1, "Submarines Prey on Unprotected Shipping"), Faction));                
                scorePointsEvent.IsTrigger = false;
                await CardPlayPool.DoChangeEvent(scorePointsEvent);
                return null;
            })
        }; 
    }
}