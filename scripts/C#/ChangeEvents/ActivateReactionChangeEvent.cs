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
        this.StepId = SourceCardState.CardLogic.CardSteps[0].Id;
    }

    public override ChangeEventDto ToDto() => ChangeEventDto.Build<ActivateReactionChangeEventDto>(this, Id);

    protected override async Task<bool> ExecuteAsync(){               
        DebugUtilities.PrintPeer($"Activating reaction card {SourceCardState.CardName} for faction {TriggeringFaction}");         
        SourceCardState.ActivatedInTurns.Add(GameFlow.Instance.GameTurn);        
        return true;
    }

    public override string SummaryText() => $"{TriggeringFaction} activated card {SourceCardState.CardName}";
}
