using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class FactionsContainer : Control
{
    public static FactionsContainer Current;

    private VBoxContainer Column => GetNode<VBoxContainer>("%FactionsColumn");
    private Panel Pole => GetNode<Panel>("%Pole");

    public Dictionary<Faction, FactionInfoRow> FactionInfoNodes = new();

    /// <summary>
    /// Whether <see cref="_Ready"/> got as far as connecting. Only the authority peer subscribes, so
    /// tearing down unconditionally would disconnect signals that were never connected.
    /// </summary>
    private bool _subscribed;

    public override void _Ready()
    {
        if(SessionIdentity.IsLocalAuthority(this))
        {
            DebugUtilities.PrintPeerFinest($"Setting up FactionsContainer for local player: {GetMultiplayerAuthority()}");
            Current = this;
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
        foreach (Node child in Column.GetChildren())
        {
            Column.RemoveChild(child);
            child.QueueFree();
        }

        FactionInfoNodes.Clear();

        // One row per faction that takes turns. A minor faction (France, China) shares its patron's row:
        // FactionInfoRow folds its score in and hangs its pieces as extra tags.
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
            Column.AddChild(rowInstance);
            FactionInfoNodes[faction] = rowInstance;
        }

        // The pole runs from the finial to just past the last pennant. Rows with a name plate stand taller.
        float rowHeight = FactionInfoNodes.Values.Sum(row => row.CustomMinimumSize.Y)
            + (playerFactions.Count - 1) * Column.GetThemeConstant("separation");
        Pole.Size = Pole.Size with { Y = rowHeight + 16f };
    }

    /// <summary>Opens or closes every pennant at once, when the Open Faction Banners setting changes mid-game.</summary>
    public void SetAllOpen(bool open)
    {
        foreach (FactionInfoRow row in FactionInfoNodes.Values)
            if (IsInstanceValid(row)) row.SetOpen(open);
    }

    public FactionInfoRow GetFactionInfoNodeForFaction(Faction faction)
    {
        return FactionInfoNodes[faction];
    }
}
