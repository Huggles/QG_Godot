using Godot;
using System;
using System.Collections.Generic;

public abstract partial class ResponseCardLogic : CardLogic
{
    public override List<CardStep> InitializePlayCardSteps()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                DeckState deckState = DeckState.ForFaction(Faction);
                deckState.ResponseCardIds.Add(CardState.Id);
                deckState.HandCardIds.Remove(CardState.Id); 
            })
        }; 
    }

    public override bool CanPlayCard()
    {
        return true;
    }
}
