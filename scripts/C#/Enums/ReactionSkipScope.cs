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

    /// <summary>
    /// Only be asked when something is genuinely activatable — a standing preference with no expiry.
    ///
    /// Unlike <see cref="TURN_STEP"/> and <see cref="ROUND"/> this is enforced entirely on the
    /// client: <c>InputRequest.ActivateCardRequestHandler</c> and
    /// <c>InputRequest.BlockReactionRequestHandler</c> answer an empty window themselves without
    /// drawing it. <see cref="GameFlow.RecordReactionSkip"/> deliberately ignores it.
    ///
    /// Host-side enforcement would be cheaper and is wrong. Suppressing the window in
    /// <c>CardPlayRound.ShouldOpenReactionWindow</c> would make the window's appearance proof that
    /// the faction holds a face-down Response card matching exactly that event — which is precisely
    /// what the always-ask rule exists to prevent. Asking and self-answering keeps every
    /// "Waiting on X input…" line the other players see exactly where it was.
    /// </summary>
    UNTIL_ACTIVATABLE,
}
