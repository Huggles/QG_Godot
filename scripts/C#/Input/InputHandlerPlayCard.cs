using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;


public partial class InputHandlerPlayCard
{
    private GameFlow gameFlow => GameFlow.Instance;

    public InputHandlerPlayCard(List<int> cardIds)
    {        
        
    }

    private void HandleItemSelected(int cardId)
    {
        FactionHandDisplay.Current.CardSelected -= HandleItemSelected;
        FactionHandDisplay.Current.Hide();        
        EventBus.Emit("CardSelected", cardId);
    }    
}
