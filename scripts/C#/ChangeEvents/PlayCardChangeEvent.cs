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
        SourceCardState.CardLogic.IsPlayed = true;                
        DeckState deckState = DeckState.ForFaction(SourceCardState.Faction);
        
        // Only discard event cards - Status and Response cards are moved to their respective lists
        // by their own PlayCardSteps logic (StatusCardLogic/ResponseCardLogic)
        if (!SourceCardState.CardLogic.IsReaction)
        {
            deckState.DiscardCard(SourceCardState.Id);
        }
        
        await Task.CompletedTask;
        return true;
    }
}
