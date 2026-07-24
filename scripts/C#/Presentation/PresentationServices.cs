/// <summary>
/// Static locator for the presentation seam. Each sink is resolved once, lazily, to a Godot
/// implementation on a GUI process or a no-op implementation on a headless/dedicated server,
/// keyed off <see cref="GameContext.IsHeadless"/>. Godot implementations touch scene-tree
/// singletons only at call time, so lazy resolution before the tree is built is safe.
/// </summary>
public static class PresentationServices
{
    private static IAnimationSink _animation;
    private static INotificationSink _notification;
    private static IWorldPresenter _world;

    public static IAnimationSink Animation
        => _animation ??= GameContext.IsHeadless ? new NullAnimationSink() : new GodotAnimationSink();

    public static INotificationSink Notification
        => _notification ??= GameContext.IsHeadless ? new NullNotificationSink() : new GodotNotificationSink();

    public static IWorldPresenter World
        => _world ??= GameContext.IsHeadless ? new NullWorldPresenter() : new GodotWorldPresenter();
}
