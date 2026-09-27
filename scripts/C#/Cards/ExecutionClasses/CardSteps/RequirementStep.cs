using System;
using System.Threading.Tasks;

/// <summary>
/// A cost, or the first half of one indivisible action: discard to play this, spend the play action,
/// eliminate the unit you are about to rebuild. Applied directly, so NO block window and NO
/// after-reaction window open on it.
///
/// Because nothing can be interposed between this step and the one it pays for — see the trace in
/// the plan, and <see cref="CardStep.StepSucceeded"/> for the gate that keeps the order — a cost and
/// its effect stay atomic with respect to reactions even though they are now two steps.
///
/// Pair it with <c>.RequiringPreviousStep()</c> on the step it pays for, and leave the card's gating
/// condition on THIS step: a condition on the later half would be evaluated after this event applied
/// and after CalculateAll ran, which is how you get "cost paid, effect skipped".
/// </summary>
public sealed partial class RequirementStep : EventCardStep
{
    public RequirementStep(CardLogic cardLogic, Func<Task<CardStepResult>> produce) : base(cardLogic, produce) { }

    public RequirementStep(CardLogic cardLogic, StepChoice choice) : base(cardLogic, choice) { }

    public override StepKind Kind => StepKind.Requirement;

    protected override bool EventIsTrigger => false;

    /// <summary>
    /// <c>Apply()</c>, not <c>CardPlayPool.DoChangeEvent</c>.
    ///
    /// Both are equivalent for an IsTrigger = false event as far as the windows go — DoChangeEvent
    /// gates both of them on IsTrigger — but DoChangeEvent calls RegisterChangeEvent and then
    /// Apply() self-registers again (ChangeEvent.ApplyMutation), so every cost routed that way sat
    /// in the round's ChangeEventsPool TWICE. Harmless, because every reader uses Any /
    /// LastOrDefault / OfType / Count > 0 — and ChangeEventsPoolMap, whose ToDictionary would throw
    /// on the duplicate key, is defined and never called — but there is no reason to keep it.
    ///
    /// The one thing DoChangeEvent does that Apply() does not, and that therefore has to be done
    /// here explicitly: a faction with an empty unit pool must free a unit before a deploy lands.
    /// </summary>
    protected override async Task DispatchEventAsync(ChangeEvent changeEvent)
    {
        if (changeEvent is DeployUnitChangeEvent deployEvent)
            await UnitPoolShortfall.ResolveBeforeDeploy(deployEvent);

        await changeEvent.Apply();
    }
}
