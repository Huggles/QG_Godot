using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class FactionsContainer : Control
{
    public static FactionsContainer Current;

    private HBoxContainer HorizontalContainer => GetNode<HBoxContainer>("%FactionsHorizontalContainer");

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


    /// <summary>The faction strip's minimum size as the scene sets it, before any minor-faction tabs.</summary>
    private Vector2? _sceneMinimumSize;

    private void InitChildElements()
    {
        DebugUtilities.PrintPeerFinest("FactionsContainer initializing child elements");
        foreach (Node child in HorizontalContainer.GetChildren())
        {
            HorizontalContainer.RemoveChild(child);
            child.QueueFree();
        }

        FactionInfoNodes.Clear();

        // One row per faction that takes turns. A minor faction (France, China) shares its patron's row:
        // FactionInfoRow folds its score in and hangs its pieces in a tab under the row.
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
            // Rows keep their own height when the strip grows for the minor-faction tabs below:
            // their panels are anchored to the row's bottom edge and would stretch otherwise.
            rowInstance.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
            HorizontalContainer.AddChild(rowInstance);
            FactionInfoNodes[faction] = rowInstance;
        }

        // Make room for the tallest stack of minor-faction tabs, so they sit inside the panel. Set from
        // the scene's own size rather than added to the current one: this runs again on every
        // join, assignment and session start, and adding would grow the strip each time. The base
        // game has no minor factions and keeps the scene's height.
        _sceneMinimumSize ??= HorizontalContainer.CustomMinimumSize;
        int tabs = playerFactions.Max(faction => StaticGameData.MinorsOf(faction).Count);
        HorizontalContainer.CustomMinimumSize = _sceneMinimumSize.Value + new Vector2(0, tabs * FactionInfoRow.MinorTabHeight);
    }

    public FactionInfoRow GetFactionInfoNodeForFaction(Faction faction)
    {
        return FactionInfoNodes[faction];
    }
}
