using Godot;

/// <summary>
/// Paints one HUD surface with the faction the client is currently busy with, so the player can tell
/// at a glance whose turn in the interface it is — see <see cref="FactionFocus"/> for what "busy"
/// means and who claims it.
///
/// A shared helper rather than the same code on each surface, because the fiddly parts are the same
/// wherever the focus is shown: the stylebox has to be taken private before it is touched, the first
/// paint has to happen at attach time rather than waiting for a change, and overlapping claims have
/// to cancel each other's tween. <see cref="FactionsContainer"/> tints the plate the faction rows sit
/// on; <see cref="PlayerActionLabel"/> tints the banner at the bottom of the screen.
/// </summary>
public class FactionFocusTint
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

    public FactionFocusTint(Control panel)
    {
        _panel = panel;
    }

    /// <summary>
    /// Take the surface's stylebox private and paint whatever the client is already busy with — the
    /// tint is driven by a change signal, so a surface built after the focus was claimed (a rejoin, a
    /// UI rebuild mid-prompt) would otherwise sit at its authored colour until the next change.
    /// </summary>
    public void Attach()
    {
        if (_subscribed || _panel == null) return;

        _styleBox = _panel.GetThemeStylebox("panel") as StyleBoxFlat;
        if (_styleBox == null) return;

        _styleBox = (StyleBoxFlat)_styleBox.Duplicate();
        _panel.AddThemeStyleboxOverride("panel", _styleBox);

        Apply(FactionFocus.Current, animate: false);

        EventBus.Instance.FactionFocusChanged += OnFactionFocusChanged;
        _subscribed = true;
    }

    /// <summary>
    /// Drop the subscription. Must be called from the owner's <c>_ExitTree</c>: EventBus is a
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

        EventBus.Instance.FactionFocusChanged -= OnFactionFocusChanged;
    }

    private void OnFactionFocusChanged(int faction) => Apply((Faction)faction, animate: true);

    private void Apply(Faction faction, bool animate)
    {
        if (_styleBox == null) return;

        Color? tint = TintFor(faction);
        // Nothing in focus leaves the surface on the last faction it showed rather than fading back to
        // the scene's grey. The client passes through idle between almost every two things it does —
        // a prompt closing and the next one opening, a browse handed back — and blinking grey in those
        // gaps read as the display losing track rather than as the client being between jobs.
        if (tint is not Color target || _styleBox.BgColor == target) return;

        // Killed rather than left to finish: two tints in quick succession (a prompt closing straight
        // into the next one) would otherwise race, and the older tween would win the last frame.
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
    /// The surface's colour for <paramref name="faction"/>: its own colour held back to
    /// <see cref="FocusTintAlpha"/>, or null for a faction with no colour of its own — NONE while the
    /// client is idle, and ALL — which means "leave the surface where it is".
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
