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

    public override ChangeEventDto ToDto() => new PlayCardChangeEventDto
    {
        TriggeringFaction = TriggeringFaction, SourceCardId = SourceCardId,
        IsTrigger = IsTrigger, SuppressGameProgress = SuppressGameProgress
    };

    protected override async Task<bool> ExecuteAsync(){
        DebugUtilities.PrintPeer($"PlayCardChangeEvent.ExecuteAsync for card {SourceCardState.CardName} (id={SourceCardId})");
        DebugUtilities.PrintPeer($"  IsPlayed before move: {SourceCardState.CardLogic.IsPlayed}");
        DeckState.ForFaction(SourceCardState.Faction).PlayCard(SourceCardState.Id);
        
        DebugUtilities.PrintPeer($"  IsPlayed after move: {SourceCardState.CardLogic.IsPlayed}");
        
        await Task.CompletedTask;
        return true;
    }
}
