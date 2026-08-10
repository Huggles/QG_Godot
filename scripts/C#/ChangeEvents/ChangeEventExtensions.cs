/// <summary>
/// Fluent setters for message flags. The animation and targeting knobs are constrained to
/// <see cref="GameMessage"/> because they live on the channel and apply to a PresentationEvent just as
/// well; the reaction-chain and card-source setters stay on <see cref="ChangeEvent"/>, where those
/// concepts exist.
/// </summary>
public static class ChangeEventExtensions
{
    public static T NoTrigger<T>(this T ev) where T : ChangeEvent
    {
        ev.IsTrigger = false;
        return ev;
    }

    public static T AsTrigger<T>(this T ev) where T : ChangeEvent
    {
        ev.IsTrigger = true;
        return ev;
    }

    public static T WithoutAnimations<T>(this T ev) where T : GameMessage
    {
        ev.PlayAnimations = false;
        return ev;
    }

    public static T WithoutBlocking<T>(this T ev) where T : GameMessage
    {
        ev.BlockAnimationQueue = false;
        return ev;
    }

    public static T SuppressProgress<T>(this T ev) where T : ChangeEvent
    {
        ev.SuppressGameProgress = true;
        return ev;
    }

    public static T FromCard<T>(this T ev, int cardId) where T : ChangeEvent
    {
        ev.SourceCardId = cardId;
        return ev;
    }

    public static T Targeting<T>(this T ev, Faction targetFaction) where T : GameMessage
    {
        ev.TargetFaction = targetFaction;
        return ev;
    }
}
