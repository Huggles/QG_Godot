using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class EWBomberCommand : EWCardLogic
{
    
    public override List<CardStep> InitializePlayCardSteps()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                DiscardCardsChangeEvent discardCardsChangeEvent = BuildChangeEvent(new DiscardCardsChangeEvent(Faction, Faction.ITALY, 4));
                discardCardsChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(discardCardsChangeEvent);                
            })
        };
    }
}