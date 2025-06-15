using Godot;
using System;
using System.Collections.Generic;


public partial class InputHandlerPlayCard
{
    private GameFlow gameFlow => GameSession.Instance.GameFlow;
    private GameState gameState => GameSession.Instance.GameState;

    private List<CardActivationOption> CardActivationOptions;

    public InputHandlerPlayCard(List<CardActivationOption> cardActivationOptions)
    {
        CardActivationOptions = cardActivationOptions;
        Faction currentFaction = gameFlow.CurrentFaction;
        PlayerActionLabel.ShowText("Choose a card", currentFaction);
        InputOptionsList.ShowOptions(cardActivationOptions);
        InputOptionsList.Instance.ItemClicked += HandleItemSelected;
    }

    private void HandleItemSelected(long index, Vector2 atPosition, long mouseButtonIndex)
    {
        InputOptionsList.Instance.ItemClicked -= HandleItemSelected;
        InputOptionsList.HideList();
        int selectedIndex = (int)index;
        if (selectedIndex > CardActivationOptions.Count-1)
        {
            EventBus.Emit("CardSelected", null);
        }
        else
        {
            EventBus.Emit("CardSelected", CardActivationOptions[selectedIndex]);
        }
        
        
        
    }    
}
