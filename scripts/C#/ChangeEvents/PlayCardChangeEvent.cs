using Godot;
using System;
using System.Threading.Tasks;

public partial class PlayCardChangeEvent : ChangeEvent
{
    
    public PlayCardChangeEvent(Faction faction, int cardId) : base(faction) { 
        this.SourceCardId = cardId;
    }

    protected override async Task<bool> ExecuteAsync(){        
        SourceCardState.CardLogic.PlayCard();
        SourceCardState.CardLogic.CardFinished += () =>
        {
            DebugUtilities.PrintPeer("CardFinished");
        };
        return true;
    }
}
