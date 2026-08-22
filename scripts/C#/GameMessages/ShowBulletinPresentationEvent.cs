using System.Collections.Generic;

/// <summary>
/// Announces that a step mutator is firing, in an auto-fading modal on every peer. Emitted by
/// StepMutatorRunner immediately before it runs a mutator.
///
/// What it shows depends on where the mutator came from (IStepMutator.SourceCardId). A card put it in
/// play — a Status card on the table, or a reacting card that registered it — so the modal shows that
/// card, and a firing mutator is indistinguishable from any other card announcement. The scenario
/// declared it, so there is no card to show and the modal shows a Bulletin built from the mutator's
/// Description and BulletinText instead. The type name is that second case; it kept it because the
/// name is also this message's wire discriminator.
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

    /// <summary>The card behind the mutator, or -1 when the scenario declared it.</summary>
    public int SourceCardId { get; set; }

    public ShowBulletinPresentationEvent(Faction activeFaction, string label, string bulletinText, int sourceCardId = -1)
        : base(activeFaction)
    {
        Label = label;
        BulletinText = bulletinText;
        SourceCardId = sourceCardId;
    }

    public override PresentationEventDto ToDto()
    {
        ShowBulletinPresentationEventDto dto = PresentationEventDto.Build<ShowBulletinPresentationEventDto>(this, Id);
        dto.Label = Label;
        dto.BulletinText = BulletinText;
        dto.SourceCardId = SourceCardId;
        return dto;
    }

    /// <summary>
    /// The card gets the same modal a played or activated card gets (see PlayCardChangeEvent and
    /// ActivateReactionChangeEvent), titled with the mutator's Description — so the player is told
    /// which card is acting *and* what it is doing, which the Bulletin used to carry alone.
    /// </summary>
    protected override List<ChangeEventAnimation> Animations => SourceCardId > -1
        ? new() { new ShowCardsModalAnimation(new List<int> { SourceCardId }, Label) }
        : new() { new ShowBulletinModalAnimation(Label, BulletinText) };

    // No "Bulletin:" prefix when a card is behind it — history draws that card, so labelling the
    // entry a Bulletin would contradict what it shows.
    public override string SummaryText() => SourceCardId > -1 ? Label : $"Bulletin: {Label}";
}
