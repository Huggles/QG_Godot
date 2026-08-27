using System.Collections.Generic;

/// <summary>
/// What the local client is busy with, expressed as a faction — the one thing on screen the player is
/// currently answering for or looking at. <see cref="FactionsContainer"/> tints its backing panel with
/// it, so the player can tell at a glance whose turn in the interface it is.
///
/// A claim list rather than a single slot, for the same reason <see cref="RecallablePrompts"/> guards
/// its owner: browsing a hand happens ON TOP of a live input request (the prompt is parked, not
/// answered), so the two overlap and the browse has to hand the focus back when it closes rather than
/// blanking it. Newest claim wins; clearing a source that is no longer on top changes nothing.
/// </summary>
public static class FactionFocus
{
    private static readonly List<(FactionFocusSource Source, Faction Faction)> Claims = new();

    /// <summary>The faction currently in focus, or <see cref="Faction.NONE"/> when the client is idle.</summary>
    public static Faction Current => Claims.Count == 0 ? Faction.NONE : Claims[^1].Faction;

    /// <summary>
    /// Claim the focus for <paramref name="source"/>. A source only ever holds one claim, so re-setting
    /// an existing source moves it to the top rather than stacking a second entry.
    /// </summary>
    public static void Set(FactionFocusSource source, Faction faction)
    {
        Faction before = Current;
        Claims.RemoveAll(claim => claim.Source == source);
        if (faction != Faction.NONE)
        {
            Claims.Add((source, faction));
        }
        Notify(before);
    }

    /// <summary>Give up <paramref name="source"/>'s claim; whatever it was covering comes back.</summary>
    public static void Clear(FactionFocusSource source) => Set(source, Faction.NONE);

    /// <summary>Drop every claim — for a session torn down with prompts still nominally open.</summary>
    public static void Reset()
    {
        if (Claims.Count == 0) return;
        Faction before = Current;
        Claims.Clear();
        Notify(before);
    }

    private static void Notify(Faction before)
    {
        if (Current == before) return;
        // Null on the way out of a session, where Reset() is called as the tree is coming down.
        if (EventBus.Instance == null) return;
        EventBus.Emit(EventBus.SignalName.FactionFocusChanged, (int)Current);
    }
}

/// <summary>Who is holding <see cref="FactionFocus"/>. Listed in no particular order — the focus goes
/// to the most recent claim, not to the highest-ranked source.</summary>
public enum FactionFocusSource
{
    /// <summary>An input request this peer has to answer, including a reaction window.</summary>
    InputRequest,

    /// <summary>A hand the player pulled up from the bottom-left menu to look at.</summary>
    Browsing,
}
