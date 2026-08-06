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
/// Thrown by the epoch guards after a failure has been recovered, to make any continuation
/// belonging to the aborted pipeline unwind harmlessly instead of mutating state alongside
/// the resumed loop. Never reported.
/// </summary>
public class AbortedEpochException : Exception
{
    public AbortedEpochException(int expectedEpoch, int currentEpoch)
        : base($"Aborted: continuation belongs to game-loop epoch {expectedEpoch}, current is {currentEpoch}.") { }
}
