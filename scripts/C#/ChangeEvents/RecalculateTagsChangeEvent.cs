using System.Threading.Tasks;

/// <summary>
/// Carries a server-computed <see cref="ComputedTagsSnapshot"/> to the clients as an ordered
/// message. The server broadcasts one of these immediately after every replicated ChangeEvent
/// (see <see cref="ChangeEvent.ApplyChange"/>), so on the client it is enqueued on the
/// <see cref="ChangeEventQueue"/> right behind the change event that produced the tags — guaranteeing
/// tags are always applied to post-change state, in the same relative order on every peer.
///
/// Unlike a normal ChangeEvent it overrides <see cref="ApplyChange"/> to apply the tags directly:
/// tags are derived state, so it must NOT run the game-flow machinery (CardPlayRound registration,
/// GameChangeEvents/LatestAppliedId bookkeeping, animations) that real events run.
/// </summary>
public partial class RecalculateTagsChangeEvent : ChangeEvent
{
    public ComputedTagsSnapshot Snapshot { get; set; }

    public RecalculateTagsChangeEvent(ComputedTagsSnapshot snapshot) : base(Faction.NONE)
    {
        Snapshot = snapshot;
    }

    // Derived state only — never shown in the game history UI.
    public override bool ToHistoryItem => false;

    public override async Task<bool> ApplyChange()
    {
        GameStateCalculator.ApplyComputedTags(Snapshot);
        await Task.CompletedTask;
        return true;
    }

    // Unused: ApplyChange is overridden and never calls into the base lifecycle.
    protected override Task<bool> ExecuteAsync() => Task.FromResult(true);

    public override ChangeEventDto ToDto()
    {
        RecalculateTagsChangeEventDto dto = ChangeEventDto.Build<RecalculateTagsChangeEventDto>(this, Id);
        dto.Snapshot = Snapshot;
        return dto;
    }

    public override string SummaryText() => "Recalculated tags";
}
