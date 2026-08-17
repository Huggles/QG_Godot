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
        Guard.FireAndForget(ProcessDiscardStep, "DiscardStep", faction, stallsLoop: true);
    }

    private async Task ProcessDiscardStep()
    {
        // RequestDiscard calls BroadCast() directly, so a player pressing Skip throws
        // StepSkippedException right through this method — it is normal control flow and must
        // complete the step. Any other failure is left for Guard to report, and deliberately does
        // not fire DiscardStepFinished so the loop pauses for the player's decision.
        try
        {
            await ProcessDiscardStepInternal();
        }
        catch (StepSkippedException)
        {
            DebugUtilities.PrintPeer("Discard step selection skipped");
        }

        DiscardStepFinished?.Invoke();
    }

    private async Task ProcessDiscardStepInternal()
    {
        DeckState deckState = DeckState.ForFaction(faction);
        int handSize = deckState.HandCardIds.Count;
        int maxHandSize = StaticGameData.HandSize;

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
    }

    private async Task RequestDiscard(DeckState deckState, int minimumDiscards, bool required)
    {
        if (deckState.HandCardIds.Count == 0)
            return;
        InputRequest response = await new InputRequest.HandCardsDiscardRequestHandler(faction).BroadCast(); 

        if (response.ResponseCardIds.Count > 0)
        {
            // Discard selected cards
            List<int> sortedResponseCardIds = response.ResponseCardIds.OrderBy(id => id).ToList();
            DiscardHandCardsChangeEvent discardHandCardsChangeEvent = new DiscardHandCardsChangeEvent(faction, faction, sortedResponseCardIds);
            discardHandCardsChangeEvent.IsTrigger = false;
            await CardPlayPool.DoChangeEvent(discardHandCardsChangeEvent);
        }
        else
        {
            // No cards to discard
            await Task.Delay(GameSettings.DurationShort);
        }

        
    }
}
