using Godot;
using System;
using System.Diagnostics;

public partial class PlayStepHandlerDefault : GodotObject,IPlayStepHandler
{

    private Faction faction { get; set; }
    [Signal] public delegate void PlayStepFinishedEventHandler();

    public void Start(Faction faction){
        this.faction = faction;
        EventBus.Instance.CardPlayPoolFinished += () =>
        {
            DebugUtilities.PrintPeer("");
            EmitSignal(SignalName.PlayStepFinished);
        };

        RequestCardPlay();
    }

    public async void RequestCardPlay(){
        CardState cardState = await GameSession.RequestCardPlay(faction);
        PlayCardChangeEvent playCardChangeEvent = cardState.CardLogic.BuildChangeEvent(new PlayCardChangeEvent(faction, cardState.Id));
        playCardChangeEvent.IsTrigger = true;
        CardPlayPool.DoChangeEvent(playCardChangeEvent);
    }
}
