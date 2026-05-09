using Godot;
using System;
using System.Threading.Tasks;

public partial class PlayCardChangeEvent : ChangeEvent
{
    protected int StepId;

    public PlayCardChangeEvent(Faction faction, int cardId) : base(faction)
    {
        this.SourceCardId = cardId;
        this.StepId = SourceCardState.CardLogic.PlayCardSteps[0].Id;
    }

    protected override async Task<bool> ExecuteAsync(){
        DebugUtilities.PrintPeer($"PlayCardChangeEvent.ExecuteAsync for card {SourceCardState.CardName} (id={SourceCardId})");
        DebugUtilities.PrintPeer($"  IsPlayed before move: {SourceCardState.CardLogic.IsPlayed}");
        
        DeckState deckState = DeckState.ForFaction(SourceCardState.Faction);
        
        // Only discard event cards - Status and Response cards are moved to their respective lists
        // by their own PlayCardSteps logic (StatusCardLogic/ResponseCardLogic)
        if (!SourceCardState.CardLogic.IsReaction)
        {
            deckState.DiscardCard(SourceCardState.Id);
        }
        else
        {
            DebugUtilities.PrintPeer($"  Card is a reaction card, not discarding (will be moved by card step)");
        }
        
        DebugUtilities.PrintPeer($"  IsPlayed after move: {SourceCardState.CardLogic.IsPlayed}");
        
        await Task.CompletedTask;
        return true;
    }
}
