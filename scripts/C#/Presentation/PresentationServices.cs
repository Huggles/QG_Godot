/// <summary>
/// Static locator for the presentation seam. Each sink is resolved once, lazily, to a Godot
/// implementation on a GUI process or a no-op implementation on a headless/dedicated server, keyed
/// off <see cref="GameContext.IsHeadless"/>. Godot implementations touch scene-tree singletons only
/// at call time, so lazy resolution before the tree is built is safe.
///
/// A save-game restore redirects two of the three for the duration of the replay. That check lives on
/// the property rather than in a swap-in-swap-out Override method on purpose: an exception mid-replay
/// then cannot leave the game permanently mute.
/// </summary>
public static class PresentationServices
{
    private static IAnimationSink _animation;
    private static INotificationSink _notification;
    private static IWorldPresenter _world;

    private static readonly NullAnimationSink FastForwardAnimation = new();
    private static ReplayNotificationSink _fastForwardNotification;

    public static IAnimationSink Animation
        => ReplayContext.IsFastForwarding
            ? FastForwardAnimation
            : (_animation ??= GameContext.IsHeadless ? new NullAnimationSink() : new GodotAnimationSink());

    /// <summary>
    /// Silenced through a decorator rather than <see cref="NullNotificationSink"/> while
    /// fast-forwarding. The null sink answers <c>LocalPlayerControls</c> with a flat false, and both
    /// <c>GameAPI.DrawCards</c> and <c>CardState.IsFaceVisibleToLocalPlayer</c> branch on it — using it
    /// here would quietly make a restored game render (and log) a different game.
    /// </summary>
    public static INotificationSink Notification
    {
        get
        {
            INotificationSink real =
                _notification ??= GameContext.IsHeadless ? new NullNotificationSink() : new GodotNotificationSink();
            if (!ReplayContext.IsFastForwarding) return real;

            if (_fastForwardNotification?.Inner != real)
                _fastForwardNotification = new ReplayNotificationSink(real);
            return _fastForwardNotification;
        }
    }

    /// <summary>
    /// Never suppressed. SpawnCountries/SpawnUnits build the visual board in the game mode's Init(),
    /// which runs before a restore's replay — a GUI host silenced here would come back to an empty map.
    /// </summary>
    public static IWorldPresenter World
        => _world ??= GameContext.IsHeadless ? new NullWorldPresenter() : new GodotWorldPresenter();
}
