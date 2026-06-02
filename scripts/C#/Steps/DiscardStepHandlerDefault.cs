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

        DebugUtilities.PrintPeer($"Requesting discard for {faction} (minimum discards: {minimumDiscards}, required: {required})");
        InputRequest response = await new InputRequest.HandCardsDiscardRequestHandler(faction).BroadCast(); 
        DebugUtilities.PrintPeer($"Received discard response for {faction}: {string.Join(", ", response.ResponseCardIds)}");
        

        if (response.ResponseCardIds.Count > 0)
        {
            // Discard selected cards
            DiscardHandCardsChangeEvent discardHandCardsChangeEvent = new DiscardHandCardsChangeEvent(faction, faction, response.ResponseCardIds);
            discardHandCardsChangeEvent.IsTrigger = false;
            await CardPlayPool.DoChangeEvent(discardHandCardsChangeEvent);
        }
        else
        {
            // No cards to discard
            await Task.Delay(GameSettings.PauseDuration);
        }

        
    }
}
