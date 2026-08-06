using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

public partial class PlayStepHandlerDefault : GodotObject, IPlayStepHandler
{
    private Faction faction { get; set; }
    [Signal] public delegate void PlayStepFinishedEventHandler();

    private bool subscribed = false;

    public void Start(Faction faction)
    {
        this.faction = faction;
        EventBus.Instance.CardPlayPoolFinished += OnRoundFinished;
        subscribed = true;
        Guard.FireAndForget(() => CardPlayRound.Current.Start(faction), "PlayStep.CardPlayRound", faction);
    }

    private void OnRoundFinished()
    {
        EventBus.Instance.CardPlayPoolFinished -= OnRoundFinished;
        subscribed = false;
        EmitSignal(SignalName.PlayStepFinished);
    }

    /// <summary>
    /// Detach from the global EventBus signal. Without this, an aborted play step leaves the
    /// subscription live, and the next round to finish — including the START step's, which uses the
    /// same signal — fires this handler and advances the turn loop out of band.
    /// </summary>
    public void Cancel()
    {
        if (!subscribed) return;
        EventBus.Instance.CardPlayPoolFinished -= OnRoundFinished;
        subscribed = false;
    }
}
