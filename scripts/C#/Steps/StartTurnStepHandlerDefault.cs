using Godot;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

public partial class StartTurnStepHandler : GodotObject, IStartTurnStepHandler
{
    private Faction faction { get; set; }
    [Signal] public delegate void StartTurnStepFinishedEventHandler();

    private bool subscribed = false;

    public void Start(Faction faction)
    {
        this.faction = faction;
        EventBus.Instance.CardPlayPoolFinished += OnRoundFinished;
        subscribed = true;
        Guard.FireAndForget(() => CardPlayRound.Current.Start(faction), "StartTurnStep.CardPlayRound", faction);
    }

    private void OnRoundFinished()
    {
        EventBus.Instance.CardPlayPoolFinished -= OnRoundFinished;
        subscribed = false;
        EmitSignal(SignalName.StartTurnStepFinished);
    }

    /// <summary>Detach from the global EventBus signal — see PlayStepHandlerDefault.Cancel.</summary>
    public void Cancel()
    {
        if (!subscribed) return;
        EventBus.Instance.CardPlayPoolFinished -= OnRoundFinished;
        subscribed = false;
    }
}
