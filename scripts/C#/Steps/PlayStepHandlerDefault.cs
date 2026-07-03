using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

public partial class PlayStepHandlerDefault : GodotObject, IPlayStepHandler
{
    private Faction faction { get; set; }
    [Signal] public delegate void PlayStepFinishedEventHandler();

    public void Start(Faction faction)
    {
        this.faction = faction;
        EventBus.Instance.CardPlayPoolFinished += OnRoundFinished;
        _ = CardPlayRound.Current.Start(faction);
    }

    private void OnRoundFinished()
    {
        EventBus.Instance.CardPlayPoolFinished -= OnRoundFinished;
        EmitSignal(SignalName.PlayStepFinished);
    }
}
