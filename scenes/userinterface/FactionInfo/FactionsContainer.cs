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

    /// <summary>How much of the faction colour the plate takes. Below full so the rows in front of it
    /// stay the brightest thing in the frame, and their own faction colours still read apart from it.</summary>
    private const float FocusTintAlpha = 0.8f;

    public Dictionary<Faction, FactionInfoRow> FactionInfoNodes = new();

    /// <summary>
    /// The plate's own stylebox, duplicated once so tinting it cannot bleed into the shared resource the
    /// scene declares — every other PanelContainer using it would otherwise follow this one's colour.
    /// </summary>
    private StyleBoxFlat _panelStyleBox;

    private Tween _tintTween;

    public override void _Ready()
    {   
        if(GetMultiplayerAuthority() == Multiplayer.GetUniqueId())
        {
            DebugUtilities.PrintPeerFinest($"Setting up FactionsContainer for local player: {GetMultiplayerAuthority()}");
            Current = this;
            SetUpFocusTint();
            EventBus.Instance.PlayerJoined += InitChildElements;
            EventBus.Instance.PlayerLeft += InitChildElements;
            EventBus.Instance.FactionsAssigned += InitChildElements;        
            EventBus.Instance.GameSessionStarted += InitChildElements;
            EventBus.Instance.FactionFocusChanged += OnFactionFocusChanged;
            EventBus.Instance.UserInterfaceReady += () => {
                DebugUtilities.PrintPeerFinest("FactionsContainer received UserInterfaceReady signal, initializing child elements");
                InitChildElements();
            };
        }
        DebugUtilities.PrintPeerFinest($"FactionsContainer ready: {GetMultiplayerAuthority()}");  
    }

    public override void _ExitTree()
    {
        // Unsubscribe from events
        if (EventBus.Instance != null)
        {
            EventBus.Instance.PlayerJoined -= InitChildElements;
            EventBus.Instance.PlayerLeft -= InitChildElements;
            EventBus.Instance.FactionsAssigned -= InitChildElements;        
            EventBus.Instance.GameSessionStarted -= InitChildElements;
            EventBus.Instance.FactionFocusChanged -= OnFactionFocusChanged;
        }
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

    /// <summary>
    /// Takes the plate's stylebox private and paints whatever the client is already busy with — the tint
    /// is driven by a change signal, so a container built after the focus was claimed (a rejoin, a UI
    /// rebuild mid-prompt) would otherwise sit grey until the next change.
    /// </summary>
    private void SetUpFocusTint()
    {
        _panelStyleBox = BackgroundPanel.GetThemeStylebox("panel") as StyleBoxFlat;
        if (_panelStyleBox == null) return;

        _panelStyleBox = (StyleBoxFlat)_panelStyleBox.Duplicate();
        BackgroundPanel.AddThemeStyleboxOverride("panel", _panelStyleBox);

        ApplyFocusTint(FactionFocus.Current, animate: false);
    }

    private void OnFactionFocusChanged(int faction) => ApplyFocusTint((Faction)faction, animate: true);

    private void ApplyFocusTint(Faction faction, bool animate)
    {
        if (_panelStyleBox == null) return;

        Color? tint = TintFor(faction);
        // Nothing in focus leaves the plate on the last faction it showed rather than fading back to
        // the scene's grey. The client passes through idle between almost every two things it does —
        // a prompt closing and the next one opening, a browse handed back — and blinking grey in those
        // gaps read as the display losing track rather than as the client being between jobs.
        if (tint is not Color target || _panelStyleBox.BgColor == target) return;

        // Killed rather than left to finish: two tints in quick succession (a prompt closing straight
        // into the next one) would otherwise race, and the older tween would win the last frame.
        _tintTween?.Kill();
        _tintTween = null;

        if (!animate || !IsInsideTree())
        {
            _panelStyleBox.BgColor = target;
            return;
        }

        _tintTween = CreateTween();
        _tintTween.TweenProperty(_panelStyleBox, "bg_color", target, GameSettings.DurationShortSeconds);
    }

    /// <summary>
    /// The plate's colour for <paramref name="faction"/>: its own colour held back to
    /// <see cref="FocusTintAlpha"/>, or null for a faction with no colour of its own — NONE while the
    /// client is idle, and ALL — which means "leave the plate where it is".
    /// </summary>
    private static Color? TintFor(Faction faction)
    {
        // The static table rather than FactionState.ForEnum: the colour is scenario data and is there
        // before any game state is, and NONE/ALL simply miss the lookup instead of needing a branch.
        if (!StaticGameData.FactionDataMap.TryGetValue(faction, out FactionData factionData))
        {
            return null;
        }

        Color factionColor = factionData.FactionColor;
        return new Color(factionColor.R, factionColor.G, factionColor.B, FocusTintAlpha);
    }
}
