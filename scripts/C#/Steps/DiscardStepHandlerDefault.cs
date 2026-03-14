using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class DiscardStepHandlerDefault : GodotObject, IDiscardStepHandler
{
    private Faction faction { get; set; }
    
    public event Action DiscardStepFinished;

    public void Start(Faction faction)
    {
        this.faction = faction;
        _ = ProcessDiscardStep();
    }

    private async Task ProcessDiscardStep()
    {
        DeckState deckState = DeckState.ForFaction(faction);
        int handSize = deckState.HandCardIds.Count;
        int maxHandSize = 7;

        // Only need to discard if hand size exceeds maximum
        if (handSize > maxHandSize)
        {
            int requiredDiscards = handSize - maxHandSize;
            await RequestDiscard(deckState, requiredDiscards, true);
        }
        else
        {
            // Optional discard - player can choose to discard cards
            await RequestDiscard(deckState, 0, false);
        }

        DiscardStepFinished?.Invoke();
    }

    private async Task RequestDiscard(DeckState deckState, int minimumDiscards, bool required)
    {
        if (deckState.HandCardIds.Count == 0)
            return;

        InputHandlerDiscard inputHandler = new InputHandlerDiscard(faction, minimumDiscards, required);
        List<int> selectedCardIds = await inputHandler.GetSelectedCards();

        // Discard the selected cards
        foreach (int cardId in selectedCardIds)
        {
            deckState.DiscardCard(cardId);
        }

        if (selectedCardIds.Count > 0)
        {
            string message = required 
                ? $"Discarded {selectedCardIds.Count} cards (required: {minimumDiscards})"
                : $"Discarded {selectedCardIds.Count} cards";
            PlayerActionLabel.ShowText(message, faction);
            await Task.Delay(1500);
        }
    }
}
