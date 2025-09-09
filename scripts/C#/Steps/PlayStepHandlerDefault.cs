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
        EventBus.Instance.CardPlayPoolFinished += CardPoolFinished;
        RequestCardPlay();
    }

    public async void CardPoolFinished()
    {
        DebugUtilities.PrintPeer("");
        CardPlayPool.ClearPool();
        EmitSignal(SignalName.PlayStepFinished);
    }

    public async void RequestCardPlay()
    {
        CardActivationOption cardActivationOption = await CardPlayPool.RequestCardActivationOptions(faction);
        if (cardActivationOption == null)
        {
            CardPlayPool.ClearPool();
            CardPoolFinished();
        }

    }
}
