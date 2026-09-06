using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;

public partial class EWBomberCommand : EWCardLogic
{
    // No Targets() override: what this offers is a choice of WHICH Axis faction discards, and a
    // Faction target names no board space (InputRequest.PopulateCardTargetPreviews draws only
    // Country and Unit). The effect lands on a deck, not on the map.

    
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {

                var factionResp = await new InputRequest.SelectFactionRequestHandler(
                    Faction, new List<Faction> { Faction.GERMANY, Faction.ITALY }).BroadCast();
                Faction selectedFaction = (Faction)factionResp.ResponseCardIds[0];

                ForceDiscardCardsChangeEvent ForceDiscardCardsChangeEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, selectedFaction, 4));
                ForceDiscardCardsChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(ForceDiscardCardsChangeEvent);
            })
        };
    }
}