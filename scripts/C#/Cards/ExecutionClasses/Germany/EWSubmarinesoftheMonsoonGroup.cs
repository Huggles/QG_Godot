using System;
using System.Collections.Generic;
using Godot;

public partial class EWSubmarinesoftheMonsoonGroup : EWCardLogic
{
    // No Targets() override: see EWBomberCommand. A choice of which Allied faction discards names
    // no country and no unit, and the VP is flat — no board state feeds it.

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                // Select Allied faction via network-safe handler
                var factionResp = await new InputRequest.SelectFactionRequestHandler(
                    Faction, new List<Faction> { Faction.UNITED_KINGDOM, Faction.UNITED_STATES, Faction.SOVIET }).BroadCast();
                Faction selectedFaction = (Faction)factionResp.ResponseCardIds[0];
                
                // Selected faction discards 2 cards
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, selectedFaction, 2));
                discardEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(discardEvent);
                
                // Score 2 VP
                await CardPlayPool.DoChangeEvent(new ScorePointsChangeEvent(new VPEntry(2, "Submarines of the Monsoon Group"), Faction));
            })
        }; 
    }
}