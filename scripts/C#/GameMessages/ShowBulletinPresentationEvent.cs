using System.Collections.Generic;

/// <summary>
/// Announces that a scenario step mutator is firing, by showing its Bulletin card in an auto-fading
/// modal on every peer. Emitted by StepMutatorRunner immediately before it runs a mutator.
///
/// A <see cref="PresentationEvent"/> rather than a raw Rpc because an Rpc body runs immediately on a
/// client while replicated messages sit deferred in the ChangeEventQueue — the modal would pop before
/// or after the very effects it is announcing. Riding the ordered channel fixes that; being a
/// PresentationEvent rather than a ChangeEvent means it does so without a phantom mutation, a state
/// hash, or any reach into the reaction chain.
///
/// It needs no Rpc of its own to reach every peer: replication means each peer replays this message and
/// runs its own copy of the animation below, exactly as ForceDiscardCardsChangeEvent's reveal does.
/// </summary>
public partial class ShowBulletinPresentationEvent : PresentationEvent
{
    public string Label { get; set; }
    public string BulletinText { get; set; }

    public ShowBulletinPresentationEvent(Faction activeFaction, string label, string bulletinText) : base(activeFaction)
    {
        Label = label;
        BulletinText = bulletinText;
    }

    public override PresentationEventDto ToDto()
    {
        ShowBulletinPresentationEventDto dto = PresentationEventDto.Build<ShowBulletinPresentationEventDto>(this, Id);
        dto.Label = Label;
        dto.BulletinText = BulletinText;
        return dto;
    }

    protected override List<ChangeEventAnimation> Animations => new()
    {
        new ShowBulletinModalAnimation(Label, BulletinText)
    };

    public override string SummaryText() => $"Bulletin: {Label}";
}
