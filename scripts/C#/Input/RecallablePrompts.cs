/// <summary>
/// The single thing the bottom-left recall button can bring back on this peer, or null.
///
/// Single-slot on purpose: a peer is only ever asked one thing at a time (see
/// <c>OpeningDiscard.Run</c>, which groups its requests by controlling peer for exactly that reason),
/// so two live prompts are a bug and get logged rather than queued.
///
/// Note the deliberate asymmetry between the two kinds that use this. A card prompt owns the slot
/// while it is <em>open</em> — recall re-draws the hand after the player browsed something else over
/// it. A modal prompt owns the slot only while it is <em>parked</em> — an open modal is already on
/// screen. The slot means "there is something the button can bring back", not "a prompt exists".
/// </summary>
public static class RecallablePrompts
{
    public static IRecallablePrompt Current { get; private set; }

    public static void Set(IRecallablePrompt prompt)
    {
        if (Current == prompt) return;
        if (Current != null && prompt != null)
        {
            DebugUtilities.PrintPeerErrorRaw(
                $"RecallablePrompts: {prompt.GetType().Name} replaced a live {Current.GetType().Name}. " +
                "A peer should only ever be asked one thing at a time.");
        }
        Current = prompt;
        EventBus.Emit(EventBus.SignalName.RecallablePromptChanged);
    }

    /// <summary>
    /// Clear the slot, but only if <paramref name="prompt"/> still owns it. A stale owner tearing down
    /// after a newer prompt has taken the slot must not blank the newer one.
    /// </summary>
    public static void Clear(IRecallablePrompt prompt)
    {
        if (Current == prompt) Set(null);
    }

    public static bool Recall() => Current?.Recall() ?? false;
}
