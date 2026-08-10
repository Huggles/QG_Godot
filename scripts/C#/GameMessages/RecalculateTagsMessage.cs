using System.Threading.Tasks;

/// <summary>
/// Carries a server-computed <see cref="ComputedTagsSnapshot"/> to the clients as an ordered
/// message. The server broadcasts one of these immediately after every replicated ChangeEvent
/// (see <see cref="ChangeEvent"/>), so on the client it is enqueued on the
/// <see cref="ChangeEventQueue"/> right behind the change event that produced the tags — guaranteeing
/// tags are always applied to post-change state, in the same relative order on every peer.
///
/// A direct <see cref="GameMessage"/> rather than a ChangeEvent: tags are derived state, so it must NOT
/// run the game-flow machinery (CardPlayRound registration, history bookkeeping, hashing, animations)
/// that real events run. It used to be a ChangeEvent that overrode Apply() wholesale to skip all
/// of that; being its own branch of the stream says the same thing structurally, and it picks up the
/// stale-epoch check the override used to bypass.
/// </summary>
public partial class RecalculateTagsMessage : GameMessage
{
    public ComputedTagsSnapshot Snapshot { get; set; }

    public RecalculateTagsMessage(ComputedTagsSnapshot snapshot) : base(Faction.NONE)
    {
        Snapshot = snapshot;
    }

    // Derived state only — never shown in the game history UI.
    public override bool ToHistoryItem => false;

    public override GameMessageDto ToDto() => RecalculateTagsMessageDto.Build(this, Id);

    protected override async Task<bool> ApplyInternal(int capturedEpoch)
    {
        // Deliberately not recorded in GameMessages and not counted in LatestAppliedId: this is a
        // derived-state delivery, not an entry in the game's journal.
        ErrorReporter.ThrowIfStaleEpoch(capturedEpoch);
        GameStateCalculator.ApplyComputedTags(Snapshot);
        await Task.CompletedTask;
        return true;
    }

    public override string SummaryText() => "Recalculated tags";
}
