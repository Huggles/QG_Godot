/// <summary>
/// What one history badge and its hover popup need, captured at the moment the message was applied.
///
/// A snapshot rather than a live <see cref="GameMessage"/> reference (or an id re-resolved on hover)
/// on purpose: SummaryText() reads current state, and some messages' text moves underneath them —
/// ForceDiscardCardsChangeEvent.UndischargedCards is mutable, and RemoveUnitChangeEvent's own
/// summary depends on a UnitState.CountryId that ExecuteAsync clears. History must say what
/// happened, not what is true now. It also keeps the stream scan off the hover path:
/// GameState.GameMessages runs to thousands of entries and ChangeEvent.ForId walks all of them.
///
/// Deliberately strings and ints only — no Texture2D and no CardFace. GameHistoryItem resolves
/// <see cref="IconPath"/> and the popup builds the CardFace, both lazily, so nothing captured here
/// can drag a texture into a headless process.
/// </summary>
public readonly record struct GameHistoryEntry(
    int     Sequence,        // 1-based position in the history the player sees, shown on the badge
    int     MessageId,       // position in the replicated stream; not dense, so not what is displayed
    string  IconPath,        // null when this message type has no icon yet
    string  Summary,
    Faction Faction,
    int     SourceCardId,    // -1 when the message has no source card
    string  BulletinLabel,   // non-null only for ShowBulletinPresentationEvent
    string  BulletinText)
{
    /// <summary>
    /// The SourceCardId cast is to ChangeEvent because a PresentationEvent has none.
    /// ShowBulletinPresentationEvent is the one presentation message that reaches history, and it
    /// can still draw a card — via CardFace.Bulletin rather than a CardState.
    /// </summary>
    public static GameHistoryEntry For(GameMessage message, int sequence) => new(
        sequence,
        message.Id,
        message.HistoryIconPath(),
        message.SummaryText(),
        message.HistoryFaction(),
        (message as ChangeEvent)?.SourceCardId ?? -1,
        (message as ShowBulletinPresentationEvent)?.Label,
        (message as ShowBulletinPresentationEvent)?.BulletinText);
}
