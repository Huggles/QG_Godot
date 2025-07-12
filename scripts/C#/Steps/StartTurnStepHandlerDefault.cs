using Godot;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

public partial class StartTurnStepHandler : GodotObject,IStartTurnStepHandler
{

    private Faction faction { get; set; }
    [Signal] public delegate void StartTurnStepFinishedEventHandler();

    public void Start(Faction faction){
        this.faction = faction;
        EventBus.Instance.CardPlayPoolFinished += CardPoolFinished;
        RequestCardPlay();
    }

    public async void CardPoolFinished()
    {        
        await Task.Delay(100);
        CardPlayPool.ClearPool();
        EmitSignal(SignalName.StartTurnStepFinished); 
    }

    public async void RequestCardPlay()
    {
        bool cardPlayed = await CardPlayPool.RequestActivationOption(faction);
        if (cardPlayed == false)
        {
            await Task.Delay(100);
            CardPlayPool.ClearPool();
            CardPoolFinished();
        }

    }
}
