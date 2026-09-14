/// <summary>Which arm of <see cref="CardStepResult"/> is populated.</summary>
public enum StepOutcome
{
    /// <summary>The step ran and there was nothing to do. A legitimate answer, not a failure.</summary>
    Nothing,

    /// <summary>The step produced one ChangeEvent, in <see cref="CardStepResult.ChangeEvent"/>.</summary>
    Event,

    /// <summary>The step chose a card to play, in <see cref="CardStepResult.PlayCardId"/>.</summary>
    PlayCard,

    /// <summary>Block the event this activation was offered.</summary>
    Block,

    /// <summary>Block the event AND the whole card that produced it (ResponseASWTactics).</summary>
    BlockCard,
}

/// <summary>
/// What a card step produced. One type for every <see cref="StepKind"/>, so <see cref="CardStep"/>
/// has exactly one dispatch seam, one place the "exactly one ChangeEvent per step" invariant is
/// enforced, and one object worth logging.
///
/// **The implicit conversion is what keeps the common case honest.** Around 130 of the ~134 steps in
/// the game just hand back their ChangeEvent; wrapping each of those in a factory call would be
/// ceremony that buys nothing, and would bury the one interesting line of the step in punctuation.
/// So <c>return new DeployUnitChangeEvent(...)</c> still compiles, and only the four minority kinds
/// name their arm explicitly.
///
/// The trade that buys: a step returning the wrong arm for its kind is a runtime error rather than a
/// compile error. It is reported with the card name and step id, and it is a category of mistake a
/// card author has to go out of their way to make — a ResultStep returning
/// <see cref="PlayCard"/> reads as obviously wrong at the call site.
///
/// <see cref="Nothing"/> is deliberately NOT the same as skipping. It means "the step ran, and its
/// effect turned out to be empty" — exactly what a bare <c>return</c> out of the old
/// <c>Func&lt;Task&gt;</c> lambda meant, and what StatusBiasForAction, StatusWomenConscripts and
/// ResponseRationing rely on. <see cref="CardStep.StepSucceeded"/> still becomes true, so a partner
/// step gated on <c>RequiringPreviousStep</c> still runs. A step that must STOP its partner throws
/// <see cref="StepSkippedException"/> instead.
/// </summary>
public readonly record struct CardStepResult(StepOutcome Outcome, ChangeEvent ChangeEvent, int PlayCardId)
{
    /// <summary>The step ran and there was nothing to do. See the note on the type.</summary>
    public static readonly CardStepResult Nothing = new(StepOutcome.Nothing, null, -1);

    /// <summary>
    /// A ChangeEvent is a result on its own — see the note on the type for why this is implicit.
    /// A null event degrades to <see cref="Nothing"/> rather than throwing: a card that computes its
    /// event and finds no target has said "nothing to do", and should not be punished for saying it
    /// with a <c>null</c>.
    /// </summary>
    public static implicit operator CardStepResult(ChangeEvent changeEvent)
        => changeEvent is null ? Nothing : new(StepOutcome.Event, changeEvent, -1);

    /// <summary>Play this card, nested, through <see cref="CardPlayPool.DoCard"/>.</summary>
    public static CardStepResult PlayCard(int cardId)
        => cardId < 0 ? Nothing : new(StepOutcome.PlayCard, null, cardId);

    /// <summary>Block the event this activation was offered.</summary>
    public static CardStepResult Block() => new(StepOutcome.Block, null, -1);

    /// <summary>
    /// Block the event AND the card that produced it, so that card's remaining steps do not run.
    /// ResponseASWTactics is the only user in the base game — see ChangeEvent.IsCardBlocked.
    /// </summary>
    public static CardStepResult BlockCard() => new(StepOutcome.BlockCard, null, -1);
}
