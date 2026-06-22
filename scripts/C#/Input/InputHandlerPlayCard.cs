using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;


public partial class InputHandlerPlayCard
{
    private GameFlow gameFlow => GameFlow.Instance;

    private List<int> cardIds;

    public InputHandlerPlayCard(List<int> cardIds)
    {
        this.cardIds = cardIds;
        Faction currentFaction = gameFlow.CurrentFaction;
        PlayerActionLabel.ShowText("Choose a card", currentFaction);
        FactionHandDisplay.Current.Show(cardIds);        
        FactionHandDisplay.Current.CardSelected += HandleItemSelected;        
    }

    private void HandleItemSelected(int cardId)
    {
        FactionHandDisplay.Current.CardSelected -= HandleItemSelected;
        FactionHandDisplay.Current.Hide();        
        EventBus.Emit("CardSelected", cardId);
    }    
}
