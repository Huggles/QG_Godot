using Godot;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

public partial class PlayStepHandlerDefault : GodotObject,IPlayStepHandler
{

    private Faction faction { get; set; }
    [Signal] public delegate void PlayStepFinishedEventHandler();

    public void Start(Faction faction){
        this.faction = faction;
        EventBus.Instance.CardPlayPoolFinished += async() =>
        {
            DebugUtilities.PrintPeer("");
            await Task.Delay(100);
            CardPlayPool.ClearPool();
            EmitSignal(SignalName.PlayStepFinished);
        };

        RequestCardPlay();
    }

    public async void RequestCardPlay()
    {
        CardPlayPool.RequestActivationOption(faction);
       
    }
}
