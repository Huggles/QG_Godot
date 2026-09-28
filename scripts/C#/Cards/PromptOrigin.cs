using System;

/// <summary>
/// The card step currently executing, so <see cref="NetworkApi.SendInputRequest"/> can stamp its
/// identity onto every prompt that step raises.
///
/// Mirrors <see cref="StepMutatorRunner.RunningBulletin"/>, which already does exactly this for step
/// mutators and is read in InputRequest.BroadCast to stamp the Bulletin fields. The difference is where
/// it is read: BroadCast is NOT the chokepoint, because CardPlayRound.RequestPlay and RequestBlock call
/// SendInputRequest directly and bypass it. SendInputRequest is the one point every request passes
/// through — the same reason TutorialRuntime.ConstrainRequest hooks there and nowhere else.
///
/// **Reentrancy is the whole difficulty, and <see cref="Scope"/> is the whole answer.** A step's
/// StepLogic awaits, and a nested card can run its own steps inside that await: a reaction played in a
/// window the step opened, or the recursive NextCardStep.Execute() in the skip branch. A naive
/// <c>Current = this; ... Current = null;</c> would leave the OUTER step's context blanked the moment
/// an inner one finished. So every entry saves the frame it displaced and restores it on the way out —
/// the same discipline as CardPlayRound.DoCard's save/restore of CardLogic.ActivationTrigger. Because
/// the restore is a finally in the same method activation as the assignment, the outer frame is correct
/// whenever the outer step's own code is running, whether or not anything suspended.
///
/// **Deliberately not <see cref="System.Threading.AsyncLocal{T}"/>**, which advertises exactly these
/// semantics. ExecutionContext is only restored at an await SUSPENSION, so an async method that
/// completes fully synchronously leaks its mutation to its caller. Headless durations are 0 and
/// RandomInputProvider.Resolve returns a completed task by design, so nested steps in the sim
/// routinely complete without ever truly suspending — precisely the configuration where AsyncLocal
/// quietly stops restoring. A construct that works in the GUI and fails in the sim is the worst
/// outcome available here; an explicit try/finally has no such dependency.
/// </summary>
public static class PromptOrigin
{
    /// <summary>
    /// Who is asking. Null outside any card step. What an option would DO is not stamped: the asking
    /// step lists it (CardStep.PossibleOutcomes), which is exact where a declared purpose was a label.
    ///
    /// <c>Kind</c> says why the step is asking at all — a <see cref="StepKind.Requirement"/> prompt is a COST the card is charging, a
    /// <see cref="StepKind.Result"/> prompt is the payoff. A bot needs it to price "discard 2 to
    /// deploy 1", and before the typed steps there was no way to tell them apart without running the
    /// step. <see cref="StepKind.Result"/> is the neutral default for a synthesised frame.
    /// </summary>
    public readonly record struct Frame(int CardId, int StepId, StepKind Kind);

    /// <summary>
    /// The innermost step currently executing. Host-side only and never serialised — the request
    /// fields stamped from it are what reach a client.
    /// </summary>
    public static Frame? Current { get; private set; }

    /// <summary>
    /// Enter a card step. Call as a <c>using</c> declaration at the top of the step's execution so the
    /// compiler generates the try/finally that restores the displaced frame:
    ///
    /// <code>using PromptOrigin.Scope origin = PromptOrigin.Enter(this);</code>
    ///
    /// Null-tolerant throughout: a step whose CardLogic or CardState is not yet wired reports -1 rather
    /// than throwing. Losing the origin costs a rule its opinion; throwing here would cost the turn.
    /// </summary>
    public static Scope Enter(CardStep step)
    {
        Frame? displaced = Current;
        Current = new Frame(
            step?.CardLogic?.CardState?.Id ?? -1,
            step?.Id ?? -1,
            step?.Kind ?? StepKind.Result);
        return new Scope(displaced);
    }

    /// <summary>
    /// Restores the frame its creator displaced. Carries the previous value rather than clearing to
    /// null — see the reentrancy note on <see cref="PromptOrigin"/>; clearing is the bug this exists to
    /// prevent.
    /// </summary>
    public readonly struct Scope : IDisposable
    {
        private readonly Frame? _displaced;
        internal Scope(Frame? displaced) { _displaced = displaced; }
        public void Dispose() => Current = _displaced;
    }
}
