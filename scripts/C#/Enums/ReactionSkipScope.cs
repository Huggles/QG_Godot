/// <summary>
/// How long a faction's "stop asking me for reactions" choice lasts, chosen from the extra buttons
/// on a reaction prompt. Travels back to the host on <see cref="InputRequest.ReactionSkipScope"/>
/// and is recorded by <see cref="GameFlow.RecordReactionSkip"/>.
///
/// A scoped skip only silences the windows that exist to hide information — see
/// <c>CardPlayRound.ShouldOpenReactionWindow</c>. A face-up card that can actually react still
/// earns its prompt.
/// </summary>
public enum ReactionSkipScope
{
    /// <summary>Plain pass: this window only. The default for every non-reaction request.</summary>
    NONE,

    /// <summary>The rest of the turn step this was chosen in.</summary>
    TURN_STEP,

    /// <summary>The rest of the game round, dropped early when the faction's own turn comes round.</summary>
    ROUND,
}
