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
        Guard.FireAndForget(ProcessDrawStep, "DrawStep", faction, stallsLoop: true);
    }

    private async Task ProcessDrawStep()
    {
        // See SupplyStepHandlerDefault: a player skip completes the step, any other failure is left
        // for Guard to report and does not fire DrawStepFinished.
        try
        {
            await ProcessDrawStepInternal();
        }
        catch (StepSkippedException)
        {
            DebugUtilities.PrintPeer("Draw step selection skipped");
        }

        DrawStepFinished?.Invoke();
    }

    private async Task ProcessDrawStepInternal()
    {
        DeckState deckState = DeckState.ForFaction(faction);
        int currentHandSize = deckState.HandCardIds.Count;
        int targetHandSize = StaticGameData.HandSize;
        int cardsToDraw = targetHandSize - currentHandSize;

        if (cardsToDraw > 0)
        {
            // Draw cards back up to the steady-state hand size
            DrawCardsChangeEvent drawCardsChangeEvent = new DrawCardsChangeEvent(Faction.NONE, faction, cardsToDraw);
            drawCardsChangeEvent.IsTrigger = false;
            await CardPlayPool.DoChangeEvent(drawCardsChangeEvent);
        }
        else
        {
            // No cards to draw
            await Task.Delay(GameSettings.DurationShort);
        }
    }
}
