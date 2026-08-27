/// <summary>
/// Which kind of window a prompt's trigger context belongs to — what the player is being asked about
/// the triggering event, and therefore whether that event has happened yet.
///
/// Drives the header <see cref="TriggerContextDisplay"/> puts over the summary and the banner text
/// <c>InputManager.SetCardSelectionActive</c> shows: a BLOCK window runs before the event is applied
/// and can still stop it, an AFTER window runs once it has landed. The summaries are written in the
/// past tense (they are the same ones the history strip uses), so without this the two windows read
/// identically and a block window claims an event happened that has not.
///
/// Stamped by the host on <c>InputRequest.TriggerReactionKind</c> for the two reaction windows, and
/// carried explicitly rather than re-derived: a client holds no <c>CardPlayRound</c>, and in a block
/// window it has not even received the trigger event yet. BULLETIN never travels — it is set locally
/// by the mutator path in <c>InputRequest.Execute</c>, which knows it without asking the host.
/// </summary>
public enum TriggerKind
{
    NONE, BLOCK, AFTER, BULLETIN
}
