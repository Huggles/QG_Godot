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
                
                // Score 1 VP
                IVictoryStepHandler vpHandler = GameFlow.Instance.vpStepHandler;
                await vpHandler.ScorePoints(new VPEntry(1, "1 VP for Submarines Prey on Unprotected Shipping."));
                
                return discardEvent;
            })
        }; 
    }
}