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

    public static T WithoutAnimations<T>(this T ev) where T : ChangeEvent
    {
        ev.PlayAnimations = false;
        return ev;
    }

    public static T WithoutBlocking<T>(this T ev) where T : ChangeEvent
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

    public static T Targeting<T>(this T ev, Faction targetFaction) where T : ChangeEvent
    {
        ev.TargetFaction = targetFaction;
        return ev;
    }
}
