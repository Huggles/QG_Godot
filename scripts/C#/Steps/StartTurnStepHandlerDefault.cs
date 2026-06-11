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
        EventBus.Instance.CardPlayPoolFinished += OnRoundFinished;
        _ = new CardPlayRound().Start(faction).ContinueWith(_ => { });
    }

    private void OnRoundFinished()
    {
        EventBus.Instance.CardPlayPoolFinished -= OnRoundFinished;
        EmitSignal(SignalName.StartTurnStepFinished);
    }
}
