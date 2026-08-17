/// <summary>
/// Something this peer was asked and can put away without answering, that the bottom-left recall
/// button brings back.
///
/// Registered with <see cref="RecallablePrompts"/> so <c>BottomLeftMenu</c> reads one thing instead of
/// growing a branch per prompt kind — board country/unit selection is the next one coming.
/// </summary>
public interface IRecallablePrompt
{
    /// <summary>What the recall button's tooltip says while this prompt owns the slot.</summary>
    string RecallTooltip { get; }

    /// <summary>Bring it back on screen. False when there turned out to be nothing to bring back.</summary>
    bool Recall();
}
