using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class DrawStepHandlerDefault : GodotObject, IDrawStepHandler
{
    private Faction faction { get; set; }
    
    public event Action DrawStepFinished;

    public void Start(Faction faction)
    {
        this.faction = faction;
        _ = ProcessDrawStep();
    }

    private async Task ProcessDrawStep()
    {
        DeckState deckState = DeckState.ForFaction(faction);
        int currentHandSize = deckState.HandCardIds.Count;
        int targetHandSize = 7;
        int cardsToDraw = targetHandSize - currentHandSize;

        if (cardsToDraw > 0)
        {
            // Draw cards back to 7
            List<int> drawnCardIds = deckState.DrawCards(cardsToDraw);
            
            // Show visual feedback of drawn cards
            if (drawnCardIds.Count > 0)
            {
                string message = $"Drew {drawnCardIds.Count} card(s)";
                PlayerActionLabel.ShowText(message, faction);
                
                // Optionally show the cards that were drawn
                List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(drawnCardIds, false);
                await PresentationModal.Instance.ShowModal(presentationItems, $"{faction} drew cards");
            }
        }
        else
        {
            // No cards to draw
            await Task.Delay(GameSettings.PauseDuration);
        }

        DrawStepFinished?.Invoke();
    }
}
