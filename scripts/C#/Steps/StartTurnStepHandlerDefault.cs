using Godot;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

public partial class StartTurnStepHandler : GodotObject, IStartTurnStepHandler
{

    private Faction faction { get; set; }
    [Signal] public delegate void StartTurnStepFinishedEventHandler();

    public void Start(Faction faction)
    {
        this.faction = faction;
        EventBus.Instance.CardPlayPoolFinished += EndHandler;
        RequestCardPlay();
    }

    public async void RequestCardPlay()
    {
        CardActivationOption cardActivationOption = await CardPlayPool.RequestCardActivationOptions(faction);
        if (cardActivationOption == null)
        {
            CardPlayPool.ClearPool();
            EndHandler();
        }
    }

    public void EndHandler()
    {
        CardPlayPool.ClearPool();
        EmitSignal(SignalName.StartTurnStepFinished); 
    }

}
