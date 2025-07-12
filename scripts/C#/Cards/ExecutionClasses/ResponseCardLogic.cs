using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public abstract partial class ResponseCardLogic : CardLogic
{
    public override List<CardStep> InitializePlayCardSteps()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                DeckState deckState = DeckState.ForFaction(Faction);
                deckState.ResponseCardIds.Add(CardState.Id);
                deckState.HandCardIds.Remove(CardState.Id);
                await Task.CompletedTask;
            })
        }; 
    }
}
