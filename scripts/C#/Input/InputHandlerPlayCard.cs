using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;


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

        FactionHandDisplay.Instance.Show(cardActivationOptions);
        FactionHandDisplay.Instance.CardSelected += HandleItemSelected;
    }

    private void HandleItemSelected(int cardId)
    {
        FactionHandDisplay.Instance.CardSelected -= HandleItemSelected;
        FactionHandDisplay.Instance.Hide();
        InputOptionsList.HideList();
        CardActivationOption selectedCardActivationOption = CardActivationOptions.Find(cao => cao.CardId == cardId);        
        EventBus.Emit("CardSelected", selectedCardActivationOption);
    }    
}
