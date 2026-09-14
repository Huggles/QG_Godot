/// <summary>
/// What a card step IS, declared by its type rather than inferred from what its body happens to do.
///
/// This is the fact <see cref="CardLogic.IsFreePlayStepActivation"/> has to restate by hand today,
/// and for the reason its own summary gives: a step body used to be a <c>Func&lt;Task&gt;</c>, so the
/// only way to read a card's cost off it was to run it — far too late for a prompt-time cue. A step
/// now declares its kind statically, and the kind rides <see cref="PromptOrigin"/> onto every prompt
/// the step raises, so a bot rule can tell a cost apart from a payoff before answering it.
/// </summary>
public enum StepKind
{
    /// <summary>One ChangeEvent, <c>IsTrigger = true</c>, through the full block + after-reaction pipeline.</summary>
    Result,

    /// <summary>One ChangeEvent, <c>IsTrigger = false</c>, applied directly. A cost, or the first half of one action.</summary>
    Requirement,

    /// <summary>No ChangeEvent: marks the event this activation was offered to block.</summary>
    Block,

    /// <summary>No ChangeEvent: registers a modifier, or mutates state that is not replicated as an event.</summary>
    Effect,

    /// <summary>No ChangeEvent of its own: a nested <see cref="CardPlayPool.DoCard"/>.</summary>
    PlayCard,
}
