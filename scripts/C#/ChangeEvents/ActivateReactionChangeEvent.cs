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

    public override ChangeEventDto ToDto() => new ActivateReactionChangeEventDto
    {
        TriggeringFaction = TriggeringFaction, SourceCardId = SourceCardId,
        IsTrigger = IsTrigger, SuppressGameProgress = SuppressGameProgress,
        SourceChangeEventId = SourceChangeEvent?.Id ?? -1
    };

    protected override async Task<bool> ExecuteAsync(){                
        SourceCardState.CardLogic.ActivatedInTurns.Add(GameSession.Current.GameFlow.GameTurn);        
        return true;
    }
}
