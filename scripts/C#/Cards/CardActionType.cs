/// <summary>
/// Describes the type of card action being requested.
/// The client resolves which cards are valid by querying the appropriate tags.
/// </summary>
public enum RequestCardActionType
{
    /// <summary>
    /// Play a hand card or activate an after-reaction.
    /// Client shows Tag.IsPlayable cards (if at the start of the round) + Tag.IsAfterReaction cards.
    /// </summary>
    PlayCard,

    /// <summary>
    /// Activate a block reaction to interrupt the current change event.
    /// Client shows Tag.IsBlockReaction cards.
    /// </summary>
    BlockReaction,
}
