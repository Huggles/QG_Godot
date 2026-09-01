using System.Collections.Generic;

/// <summary>
/// The commander says something to the player, and — unless <see cref="Wait"/> is false — the game
/// holds there until CONTINUE is pressed.
///
/// A <see cref="PresentationEvent"/> rather than a direct call on CommanderMessage.Current, for the
/// reason every presentation-only broadcast in this project is one: an Rpc body runs immediately in
/// the receiving frame while replicated messages sit deferred in the ChangeEventQueue, so anything
/// that must land at a defined point relative to the effects it narrates has to ride the ordered
/// channel. It also earns a history row for free, which is the only way a player can re-read a
/// message they clicked past.
///
/// It mutates nothing, so it is deliberately NOT a ChangeEvent: no state hash, no tag recalculation,
/// and — the one that matters — it can never become CardPlayRound.LastChangeEvent and quietly steer
/// reaction ordering. See PresentationEvent's own remarks.
/// </summary>
public partial class ShowCommanderMessagePresentationEvent : PresentationEvent
{
    public string Text { get; set; }

    /// <summary>False shows the message and carries straight on, for narration that paces itself.</summary>
    public bool Wait { get; set; }

    public ShowCommanderMessagePresentationEvent(Faction activeFaction, string text, bool wait = true)
        : base(activeFaction)
    {
        Text = text;
        Wait = wait;
    }

    public override PresentationEventDto ToDto()
    {
        ShowCommanderMessagePresentationEventDto dto =
            PresentationEventDto.Build<ShowCommanderMessagePresentationEventDto>(this, Id);
        dto.Text = Text;
        dto.Wait = Wait;
        return dto;
    }

    protected override List<ChangeEventAnimation> Animations =>
        new() { new ShowCommanderMessageAnimation(Text, Wait) };

    public override string SummaryText() => $"Commander: {Text}";
}
