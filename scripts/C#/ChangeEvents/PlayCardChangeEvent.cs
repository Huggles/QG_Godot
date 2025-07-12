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
        SourceCardState.CardLogic.PlayCard(StepId);
        SourceCardState.CardLogic.CardFinished += () =>
        {
            DebugUtilities.PrintPeer("CardFinished");
        };
        await Task.CompletedTask;
        return true;
    }
}
