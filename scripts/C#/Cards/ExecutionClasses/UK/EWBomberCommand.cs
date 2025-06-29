using System;
using System.Collections.Generic;
using Godot;

public partial class EWBomberCommand : EWCardLogic
{
public override List<CardStep> InitializePlayCardSteps()
    {
        return new List<CardStep>
        {
            new CardStep(this, ()=>{
                DiscardCardsChangeEvent discardCardsChangeEvent = BuildChangeEvent(new DiscardCardsChangeEvent(Faction, Faction.ITALY, 4));
                discardCardsChangeEvent.IsTrigger = true;
                CardPlayPool.DoChangeEvent(discardCardsChangeEvent);
            })
        }; 
    }
}