/// <summary>
/// The hand the player pulled up from the mini menu to look at, or NONE when they have not.
///
/// A static rather than a property on <c>MiniMenu</c> because the parts that must not draw over
/// a browsed hand are elsewhere in the tree: <see cref="FactionHandDisplay"/> checks it before letting
/// a prompt or the turn follower take the display back, and there is no accessor from there to the
/// menu node. The menu owns the value; everyone else only asks.
///
/// Browsing is claimed ON TOP of a live input request — the prompt is parked, not answered — so this
/// being set does not mean there is no prompt underneath.
/// </summary>
public static class HandBrowsing
{
    /// <summary>The faction whose hand is on the display, or <see cref="Faction.NONE"/>.</summary>
    public static Faction Current { get; set; } = Faction.NONE;

    /// <summary>Whether a hand is being browsed right now.</summary>
    public static bool IsActive => Current != Faction.NONE;

    /// <summary>Drop the state, for a session torn down mid-browse.</summary>
    public static void Reset() => Current = Faction.NONE;
}
