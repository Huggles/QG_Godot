using Godot;
using System;
using System.Threading.Tasks;

public partial class ActivateReactionChangeEvent : PlayCardChangeEvent
{
    private ChangeEvent SourceChangeEvent;

    public ActivateReactionChangeEvent(Faction faction, int cardId, ChangeEvent sourceChangeEvent) : base(faction, cardId)
    {
        this.SourceChangeEvent = sourceChangeEvent;
        this.SourceCardId = cardId;
    }

    protected override async Task<bool> ExecuteAsync(){        
        await SourceCardState.CardLogic.ReactTo(SourceChangeEvent);        
        return true;
    }
}
