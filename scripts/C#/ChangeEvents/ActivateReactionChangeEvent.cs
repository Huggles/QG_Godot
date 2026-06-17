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

    public override ChangeEventDto ToDto() => ChangeEventDto.Build<ActivateReactionChangeEventDto>(this, Id);

    protected override async Task<bool> ExecuteAsync(){                
        SourceCardState.CardLogic.ActivatedInTurns.Add(GameFlow.Instance.GameTurn);        
        return true;
    }

    public override string SummaryText() => $"{TriggeringFaction} activated card {SourceCardState.CardName}";
}
