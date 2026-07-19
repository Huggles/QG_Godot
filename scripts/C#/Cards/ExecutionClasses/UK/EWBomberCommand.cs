using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;

public partial class EWBomberCommand : EWCardLogic
{
    
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
                return ForceDiscardCardsChangeEvent;      
            })
        };
    }
}