using System;

/// <summary>
/// What a prompt is FOR, declared by the step that raises it.
///
/// Needed because an <see cref="InputRequest.SelectCountryRequestHandler"/> is byte-identical whether
/// BuildArmy wants a deploy target, StatusConscription wants a recruit space, or a card wants the
/// country one of your own units is standing in — roughly forty call sites, all constructed the same
/// way. A bot rule about WHERE TO BUILD cannot work without being told which of those it is looking at.
///
/// The two tempting ways to infer it instead are both worse than useless:
///
///  - Matching the step's ActionGuidance string. That is player-facing copy, interpolated at runtime:
///    the real corpus includes "Build an army", "Recruit an Army in the Balkans", "Select where to
///    rebuild the German Army" and $"Build an army in {country.Label}". No stable token, and
///    EventGunsandButter raises a build prompt AND a battle prompt from one step under one guidance
///    string. A copy edit would silently change bot behaviour with no compile error.
///  - Inferring from the offer set. CountryState.CanRecruit is "empty or occupied by my own team", so
///    EVERY country holding one of your own units is recruitable by definition. The prompts that ask
///    "which of your own units' spaces?" therefore pass a "looks like a build" test, and a
///    prefer-vacant rule would invert on them — preferring to eliminate a unit from an EMPTY space.
///    An undeclared purpose degrades to today's random behaviour; an inferred one degrades to
///    confidently wrong.
/// </summary>
public enum PromptPurpose
{
    /// <summary>
    /// Undeclared. Every rule that reads a purpose MUST treat this as "do not fire", so a step nobody
    /// has annotated behaves exactly as it did before this existed.
    /// </summary>
    NONE,

    /// <summary>Where to place a unit — a build, a recruit, or a rebuild.</summary>
    DEPLOY_TARGET,

    /// <summary>What to attack.</summary>
    BATTLE_TARGET,

    /// <summary>Which unit to remove, whether the player's own or an enemy's.</summary>
    REMOVE_TARGET,

    /// <summary>Where to move an existing unit to.</summary>
    RELOCATE_TARGET,
}

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
    /// <summary>Who is asking, and what for. Null outside any card step.</summary>
    public readonly record struct Frame(int CardId, int StepId, PromptPurpose Purpose);

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
            step?.Purpose ?? PromptPurpose.NONE);
        return new Scope(displaced);
    }

    /// <summary>
    /// Re-declare the purpose for part of a step, keeping the card and step identity.
    ///
    /// For the places where the step is not a fine enough unit — EventGunsandButter's single step
    /// raises a SelectOption and then EITHER a build target OR a battle target, so no per-step purpose
    /// can describe both — and for the two prompt raisers that run outside any step at all
    /// (MutatorRedeployAfterPlayCard, UnitPoolShortfall), where there is no frame to keep and one is
    /// synthesised.
    /// </summary>
    public static Scope Narrow(PromptPurpose purpose)
    {
        Frame? displaced = Current;
        Current = displaced is { } frame
            ? new Frame(frame.CardId, frame.StepId, purpose)
            : new Frame(-1, -1, purpose);
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
