using Godot;
using System;
using System.Collections.Generic;


public partial class InputHandlerPlayCard
{
    

    private static readonly Key[] HAND_CARD_KEYS = {
        Key.Key0, Key.Key1, Key.Key2, Key.Key3, Key.Key4, Key.Key5, Key.Key6
    };

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
        InputOptionsList.HideList();
        int selectedIndex = (int)index;
        InputOptionsList.Instance.ItemClicked -= HandleItemSelected;
        EventBus.Emit("CardSelected", CardActivationOptions[selectedIndex]);
        
    }    
}
