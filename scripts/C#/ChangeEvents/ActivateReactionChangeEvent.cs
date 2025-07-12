using Godot;
using System;
using System.Threading.Tasks;

public partial class ActivateReactionChangeEvent : ChangeEvent
{
    private ChangeEvent SourceChangeEvent;
    protected int StepId;


    public ActivateReactionChangeEvent(Faction faction, int cardId, ChangeEvent sourceChangeEvent) : base(faction)
    {
        this.SourceChangeEvent = sourceChangeEvent;
        this.SourceCardId = cardId;
        this.StepId = SourceCardState.CardLogic.ReactCardSteps[0].Id;
    }

    protected override async Task<bool> ExecuteAsync(){                
        SourceCardState.CardLogic.ActivatedInTurns.Add(GameSession.Instance.GameFlow.GameTurn);
        await SourceCardState.CardLogic.React(StepId);
        SourceCardState.CardLogic.CardFinished += () =>
        {
            DebugUtilities.PrintPeer("CardFinished");
        };
        return true;
    }
}
