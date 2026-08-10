using System.Collections.Generic;

/// <summary>
/// Shows a one-line guidance label on every peer — "Unable to: …", a step's ActionGuidance, "Skipped: …".
///
/// Replaces the raw NetworkApi.ShowPlayerActionLabel Rpc these call sites used to fire. That Rpc ran
/// immediately in the receiving client's frame while replicated messages sat deferred in the
/// ChangeEventQueue, so the label routinely appeared ahead of the effects it described. Riding the
/// ordered channel fixes that; it is the same reason StepMutatorRunner abandoned the Rpc for Bulletins.
///
/// The underlying ShowNotificationLabelAnimation sets BlockQueue = false, so the label is ordered
/// against the stream without stalling it — a passing note should not pause the game the way a Bulletin
/// modal deliberately does.
/// </summary>
public partial class ShowActionLabelPresentationEvent : PresentationEvent
{
    public string Text { get; set; }

    public ShowActionLabelPresentationEvent(Faction triggeringFaction, string text) : base(triggeringFaction)
    {
        Text = text;
    }

    // Ephemeral guidance, not a game fact — the history list would fill with "Skipped: …" noise.
    public override bool ToHistoryItem => false;

    public override PresentationEventDto ToDto()
    {
        ShowActionLabelPresentationEventDto dto = PresentationEventDto.Build<ShowActionLabelPresentationEventDto>(this, Id);
        dto.Text = Text;
        return dto;
    }

    protected override List<ChangeEventAnimation> Animations => new()
    {
        new ShowNotificationLabelAnimation(Text, TriggeringFaction)
    };

    public override string SummaryText() => Text;
}
