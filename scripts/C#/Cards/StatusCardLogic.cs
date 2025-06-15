using Godot;
using System;

public abstract partial class StatusCardLogic : CardLogic
{
    public override void InitialPlayStep()
    {
        DeckState deckState = DeckState.ForFaction(Faction);
        deckState.StatusCardIds.Add(CardState.Id);
        deckState.HandCardIds.Remove(CardState.Id);        
    }
}
