using Godot;
using System;
using System.Collections.Generic;

public partial class FactionsContainer : Control
{
    public static FactionsContainer Current;

    private HBoxContainer HorizontalContainer => GetNode<HBoxContainer>("%FactionsHorizontalContainer");

    /// <summary>
    /// The plate the faction rows sit on. It is a little larger than the rows themselves, so tinting it
    /// reads as a frame around them rather than as a change to any one row — which is what makes it the
    /// right surface for "the client is busy with this faction".
    /// </summary>
    private PanelContainer BackgroundPanel => GetNode<PanelContainer>("PanelContainer");

    public Dictionary<Faction, FactionInfoRow> FactionInfoNodes = new();

    /// <summary>Paints the plate with whatever the client is busy with. See <see cref="FactionFocusTint"/>.</summary>
    private FactionFocusTint _focusTint;

    /// <summary>
    /// Whether <see cref="_Ready"/> got as far as connecting. Only the authority peer subscribes, so
    /// tearing down unconditionally would disconnect signals that were never connected.
    /// </summary>
    private bool _subscribed;

    public override void _Ready()
    {
        if(GetMultiplayerAuthority() == Multiplayer.GetUniqueId())
        {
            DebugUtilities.PrintPeerFinest($"Setting up FactionsContainer for local player: {GetMultiplayerAuthority()}");
            Current = this;
            _focusTint = new FactionFocusTint(BackgroundPanel);
            _focusTint.Attach();
            EventBus.Instance.PlayerJoined += InitChildElements;
            EventBus.Instance.PlayerLeft += InitChildElements;
            EventBus.Instance.FactionsAssigned += InitChildElements;
            EventBus.Instance.GameSessionStarted += InitChildElements;
            EventBus.Instance.UserInterfaceReady += OnUserInterfaceReady;
            _subscribed = true;
        }
        DebugUtilities.PrintPeerFinest($"FactionsContainer ready: {GetMultiplayerAuthority()}");
    }

    public override void _ExitTree()
    {
        // EventBus is a process-wide static, so anything left connected here outlives the game scene.
        // That is not merely a leak: a Godot C# signal invokes all of its handlers through ONE
        // multicast delegate, so the first stale handler to throw ObjectDisposedException aborts the
        // whole emission and every handler behind it — including the next game's — is silently
        // skipped. That is exactly how quitting and loading a second game left the faction strip empty.
        if (Current == this) Current = null;

        _focusTint?.Detach();
        _focusTint = null;

        if (!_subscribed || EventBus.Instance == null) return;
        _subscribed = false;

        EventBus.Instance.PlayerJoined -= InitChildElements;
        EventBus.Instance.PlayerLeft -= InitChildElements;
        EventBus.Instance.FactionsAssigned -= InitChildElements;
        EventBus.Instance.GameSessionStarted -= InitChildElements;
        // A named method, not the lambda this used to be: `-=` can only match a delegate built from
        // the same method, and there is no way to name an inline closure at teardown.
        EventBus.Instance.UserInterfaceReady -= OnUserInterfaceReady;
    }

    private void OnUserInterfaceReady()
    {
        DebugUtilities.PrintPeerFinest("FactionsContainer received UserInterfaceReady signal, initializing child elements");
        InitChildElements();
    }


    private void InitChildElements()
    {
        DebugUtilities.PrintPeerFinest("FactionsContainer initializing child elements");
        foreach (Node child in HorizontalContainer.GetChildren())
        {
            HorizontalContainer.RemoveChild(child);
            child.QueueFree();
        }

        FactionInfoNodes.Clear();

        // Only show factions controlled by the local player
        List<Faction> playerFactions = StaticGameData.PlayableFactions;
        
        // If no factions assigned yet, don't show any rows
        if (playerFactions.Count == 0)
        {
            return;
        }

        foreach (Faction faction in playerFactions)
        {
            FactionInfoRow rowInstance = AssetRepository.FactionInfoRowScenePacked.Instantiate<FactionInfoRow>();
            rowInstance.Faction = faction;
            HorizontalContainer.AddChild(rowInstance);
            FactionInfoNodes[faction] = rowInstance;
        }
    }

    public FactionInfoRow GetFactionInfoNodeForFaction(Faction faction)
    {
        return FactionInfoNodes[faction];
    }
}
