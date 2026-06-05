using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;


public partial class InputHandlerPlayCard
{
    private GameFlow gameFlow => GameFlow.Instance;

    private List<CardActivationOption> CardActivationOptions;

    public InputHandlerPlayCard(List<CardActivationOption> cardActivationOptions)
    {
        CardActivationOptions = cardActivationOptions;
        Faction currentFaction = gameFlow.CurrentFaction;
        PlayerActionLabel.ShowText("Choose a card", currentFaction);
        FactionHandDisplay.Current.Show(cardActivationOptions);        
        FactionHandDisplay.Current.CardSelected += HandleItemSelected;
    }

    private void HandleItemSelected(int cardId)
    {
        FactionHandDisplay.Current.CardSelected -= HandleItemSelected;
        FactionHandDisplay.Current.Hide();        
        CardActivationOption selectedCardActivationOption = CardActivationOptions.Find(cao => cao.CardId == cardId);        
        EventBus.Emit("CardSelected", selectedCardActivationOption.StepId);
    }    
}
