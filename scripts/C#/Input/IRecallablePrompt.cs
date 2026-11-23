/// <summary>
/// Something this peer was asked and can put away without answering, that the "Show my action"
/// button brings back.
///
/// Registered with <see cref="RecallablePrompts"/> so <c>MiniMenu</c> reads one thing instead of
/// growing a branch per prompt kind — board country/unit selection is the next one coming.
/// </summary>
public interface IRecallablePrompt
{
    /// <summary>What the button's tooltip says while this prompt owns the slot.</summary>
    string RecallTooltip { get; }

    /// <summary>Already visible, so the button stays hidden: there is nothing to bring back yet.</summary>
    bool IsOnScreen { get; }

    /// <summary>Bring it back on screen. False when there turned out to be nothing to bring back.</summary>
    bool Recall();
}
