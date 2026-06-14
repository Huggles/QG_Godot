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
            DrawCardsChangeEvent drawCardsChangeEvent = new DrawCardsChangeEvent(Faction.NONE, faction, cardsToDraw);
            drawCardsChangeEvent.IsTrigger = false;
            await CardPlayPool.DoChangeEvent(drawCardsChangeEvent);
        }
        else
        {
            // No cards to draw
            await Task.Delay(GameSettings.DurationShort);
        }

        DrawStepFinished?.Invoke();
    }
}
