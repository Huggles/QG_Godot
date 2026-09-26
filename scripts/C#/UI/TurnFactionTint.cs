using Godot;

/// <summary>
/// Paints one HUD surface with the colour of the faction whose turn it is, so the player can tell at
/// a glance who the game is on without reading anything.
///
/// The turn faction, deliberately, and not the faction this peer is being prompted for: those are
/// different things and diverge constantly — a reaction window prompts the whole opposing team while
/// the acting faction's turn is still running, and a card like Murmansk Convoy prompts a faction that
/// is not on turn at all. Tinting by the prompt meant the strip said "Italy" through an Italian block
/// window on the US turn, which reads as the display having lost track.
///
/// A shared helper rather than the same code on each surface, because the fiddly parts are the same
/// wherever the turn is shown: the stylebox has to be taken private before it is touched, the first
/// paint has to happen at attach time rather than waiting for a change, and two changes in quick
/// succession have to cancel each other's tween. <see cref="FactionsContainer"/> tints the plate the
/// faction rows sit on; <see cref="PlayerActionLabel"/> tints the banner at the bottom of the screen.
/// </summary>
public class TurnFactionTint
{
    /// <summary>How much of the faction colour the surface takes. Below full so whatever sits in front
    /// of it stays the brightest thing in the frame, and its own faction colours still read apart.</summary>
    private const float FocusTintAlpha = 0.8f;

    private readonly Control _panel;

    /// <summary>
    /// The surface's own stylebox, duplicated once so tinting it cannot bleed into the shared resource
    /// the scene declares — every other panel using it would otherwise follow this one's colour.
    /// </summary>
    private StyleBoxFlat _styleBox;

    private Tween _tween;

    /// <summary>
    /// Whether <see cref="Attach"/> got as far as connecting. Not every owner attaches (only the
    /// authority peer does, for one), so tearing down unconditionally would disconnect a signal that
    /// was never connected.
    /// </summary>
    private bool _subscribed;

    public TurnFactionTint(Control panel)
    {
        _panel = panel;
    }

    /// <summary>
    /// Take the surface's stylebox private and paint the turn as it already stands — the tint is driven
    /// by change signals, so a surface built mid-game (a rejoin, a UI rebuild) would otherwise sit at
    /// its authored colour until the next turn rolled over.
    /// </summary>
    public void Attach()
    {
        if (_subscribed || _panel == null) return;

        _styleBox = _panel.GetThemeStylebox("panel") as StyleBoxFlat;
        if (_styleBox == null) return;

        _styleBox = (StyleBoxFlat)_styleBox.Duplicate();
        _panel.AddThemeStyleboxOverride("panel", _styleBox);

        Apply(animate: false);

        // The same pair, and for the same reasons, as GameRoundLabel — the other readout derived from
        // GameTurn. NOT NewTurnStarted: that is emitted from GameFlow.StartNewTurn, which only the host
        // runs, so a client would sit on the first faction's colour forever. GameChangeEventAfter fires
        // on every peer after every applied ChangeEvent, ChangeRoundChangeEvent included, and so does a
        // save replay. NextStepStarted comes off the replicated TurnStepCounter setter and catches the
        // post-restore resume, where the turn has already moved before any new event lands.
        EventBus.Instance.GameChangeEventAfter += OnGameChangeEventAfter;
        EventBus.Instance.NextStepStarted += OnNextStepStarted;
        _subscribed = true;
    }

    /// <summary>
    /// Drop the subscriptions. Must be called from the owner's <c>_ExitTree</c>: EventBus is a
    /// process-wide static, so a handler left connected here outlives the game scene — and a Godot C#
    /// signal invokes all of its handlers through ONE multicast delegate, so the first stale handler
    /// to throw ObjectDisposedException aborts the whole emission and silently skips every handler
    /// behind it, including the next game's.
    /// </summary>
    public void Detach()
    {
        _tween?.Kill();
        _tween = null;

        if (!_subscribed || EventBus.Instance == null) return;
        _subscribed = false;

        EventBus.Instance.GameChangeEventAfter -= OnGameChangeEventAfter;
        EventBus.Instance.NextStepStarted -= OnNextStepStarted;
    }

    private void OnGameChangeEventAfter(string changeEventName) => Apply(animate: true);

    private void OnNextStepStarted(int turnStep) => Apply(animate: true);

    /// <summary>
    /// Repaint from state. Runs on every ChangeEvent, so it leaves the surface alone when the turn has
    /// not moved — the equality check below is what keeps that from restarting a tween per event.
    /// </summary>
    private void Apply(bool animate)
    {
        if (_styleBox == null) return;

        Color? tint = TintFor(TurnFaction());
        if (tint is not Color target || _styleBox.BgColor == target) return;

        // Killed rather than left to finish: two turns in quick succession (a bot turn answered in the
        // frame it started) would otherwise race, and the older tween would win the last frame.
        _tween?.Kill();
        _tween = null;

        if (!animate || !_panel.IsInsideTree())
        {
            _styleBox.BgColor = target;
            return;
        }

        _tween = _panel.CreateTween();
        _tween.TweenProperty(_styleBox, "bg_color", target, GameSettings.DurationShortSeconds);
    }

    /// <summary>
    /// The faction on turn, or NONE before there is one. GameFlow.CurrentFaction answers GERMANY at
    /// turn 0 rather than admitting there is no turn yet, so the lobby and the opening discard have to
    /// be excluded here — they leave the surface at the colour the scene authored.
    /// </summary>
    private static Faction TurnFaction()
    {
        GameFlow gameFlow = GameFlow.Instance;
        if (gameFlow == null || gameFlow.GameTurn <= 0) return Faction.NONE;
        return gameFlow.CurrentFaction;
    }

    /// <summary>
    /// The surface's colour for <paramref name="faction"/>: its own colour held back to
    /// <see cref="FocusTintAlpha"/>, or null for a faction with no colour of its own — NONE before the
    /// first turn, and ALL — which both mean "leave the surface where it is".
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
