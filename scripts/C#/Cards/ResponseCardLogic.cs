using Godot;
using System;

public abstract partial class ResponseCardLogic : CardLogic
{
    public override void InitialPlayStep()
    {
        DeckState deckState = DeckState.ForFaction(Faction);
        deckState.ResponseCardIds.Add(CardState.Id);
        deckState.HandCardIds.Remove(CardState.Id);        
    }
}
