using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Announces that a scenario step mutator is firing, by showing its Bulletin card in an auto-fading
/// modal on every peer. Emitted by StepMutatorRunner immediately before it runs a mutator.
///
/// Mutates nothing — it exists purely to present. It reaches every player the same way
/// ForceDiscardCardsChangeEvent's reveal does: replication means each peer replays ApplyChange and
/// runs its own AfterAnimations, so no RPC is involved.
///
/// A ChangeEvent rather than an RPC for the same reason RecalculateTagsChangeEvent is one: a raw RPC
/// is not ordered against the queued change-event stream on a client, so the modal could pop before or
/// after the very effects it is announcing.
/// </summary>
public partial class ShowBulletinChangeEvent : ChangeEvent
{
    public string Label { get; set; }
    public string BulletinText { get; set; }

    public ShowBulletinChangeEvent(Faction activeFaction, string label, string bulletinText) : base(activeFaction)
    {
        Label = label;
        BulletinText = bulletinText;
    }

    /// <summary>
    /// Kept out of the card play pool: it changes no state, and being registered would make it the
    /// round's LastChangeEvent, which decides reaction request order and the self-block guard.
    /// </summary>
    protected override bool RegisterInCardPlayPool => false;

    public override ChangeEventDto ToDto()
    {
        ShowBulletinChangeEventDto dto = ChangeEventDto.Build<ShowBulletinChangeEventDto>(this, Id);
        dto.Label = Label;
        dto.BulletinText = BulletinText;
        return dto;
    }

    protected override async Task<bool> ExecuteAsync()
    {
        // Nothing to apply — the whole effect is the animation below, so the state hash is unchanged
        // on every peer.
        await Task.CompletedTask;
        return true;
    }

    protected override List<ChangeEventAnimation> AfterAnimations => new()
    {
        new ShowBulletinModalAnimation(Label, BulletinText)
    };

    public override string SummaryText() => $"Bulletin: {Label}";
}
