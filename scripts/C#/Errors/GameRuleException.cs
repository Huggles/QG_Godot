using System;

/// <summary>
/// Base for failures caused by game rules / card authoring disagreeing with the API's
/// preconditions — e.g. a CardStep's <c>Conditions</c> allow a deploy into a country that
/// <c>GameAPI</c> then refuses. These are real bugs worth surfacing, but they are thrown
/// *before* any state mutation, so they are always recoverable.
///
/// Existing as a base type lets <see cref="ErrorReporter"/> classify them without matching
/// on exception messages.
/// </summary>
public class GameRuleException : Exception
{
    public GameRuleException(string message) : base(message) { }
}

/// <summary>
/// Reported — never thrown — when the host's input backstop expires: the peer controlling the
/// requested faction has not answered within <c>NetworkApi.InputResponseTimeoutMs</c>.
///
/// It is a report rather than a throw because <c>NetworkApi.SendInputRequest</c> keeps holding the
/// step's await across the popup, which is what parks the turn loop and lets Retry re-send the SAME
/// request. Throwing instead would unwind the step, and Retry would then have to replay a step that
/// is not idempotent — a mutator that already removed a unit would remove a second one.
///
/// Nothing has mutated when this is built, so it is always recoverable.
/// </summary>
public class InputTimeoutException : Exception
{
    public InputTimeoutException(string message) : base(message) { }
}

/// <summary>
/// Thrown by the epoch guards after a failure has been recovered, to make any continuation
/// belonging to the aborted pipeline unwind harmlessly instead of mutating state alongside
/// the resumed loop. Never reported.
/// </summary>
public class AbortedEpochException : Exception
{
    public AbortedEpochException(int expectedEpoch, int currentEpoch)
        : base($"Aborted: continuation belongs to game-loop epoch {expectedEpoch}, current is {currentEpoch}.") { }
}
