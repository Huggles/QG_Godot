using System;
using System.Threading.Tasks;

/// <summary>
/// One ChangeEvent applied directly, so NO block window and NO after-reaction window open on it: the
/// first half of one action (eliminate the unit you are about to rebuild, take back the card you are
/// about to play), or an effect that must open no window (shuffle, grant supply).
///
/// **Never a price.** A price goes on the step it pays for with <c>.WithPrice</c> (StepPrice), so a card
/// cancelled before its effect never pays it. Pair a first half with <c>.RequiringPreviousStep()</c> on
/// the second, and leave the card's gating condition on THIS step.
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
    protected override Task DispatchEventAsync(ChangeEvent changeEvent) => ApplyWithoutWindow(changeEvent);

    /// <summary>Apply with no window, freeing a unit first for a deploy into an empty pool. Prices go this way too.</summary>
    internal static async Task ApplyWithoutWindow(ChangeEvent changeEvent)
    {
        if (changeEvent is DeployUnitChangeEvent deployEvent)
            await UnitPoolShortfall.ResolveBeforeDeploy(deployEvent);

        await changeEvent.Apply();
    }
}
