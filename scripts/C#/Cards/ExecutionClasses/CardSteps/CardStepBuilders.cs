using System;
using System.Collections.Generic;

/// <summary>
/// The fluent builders every card uses, as generic extension methods.
///
/// **Why not instance methods on <see cref="CardStep"/>.** They would return <c>CardStep</c>, so the
/// chain would widen to the base type at the first call and a subclass could never add a builder of
/// its own — <c>new ResultStep(...).WithGuidance(...).SomethingResultStepSpecific()</c> would stop
/// compiling. A generic extension method returns exactly the receiver's static type at any depth of
/// inheritance, needs no CRTP self-type parameter on the base, and survives
/// <see cref="BlockStep{TTrigger}"/>'s own type parameter, which a
/// <c>CardStep&lt;TSelf&gt;</c> base makes ugly.
///
/// The fields these set are <c>internal</c> on CardStep for the same reason, which has a happy side
/// effect: System.Text.Json ignores non-public members, so the authored-card data they hold cannot
/// leak into the saved game or the state hash even if someone forgets a [JsonIgnore].
/// </summary>
public static class CardStepBuilders
{
    public static T WithConditions<T>(this T step, Func<List<Condition>> getConditionsMethod) where T : CardStep
    {
        step.GetConditionsMethod = getConditionsMethod;
        return step;
    }

    public static T WithCondition<T>(this T step, Func<Condition> getConditionMethod) where T : CardStep
    {
        step.GetConditionsMethod = () => new List<Condition> { getConditionMethod() };
        return step;
    }

    /// <summary>
    /// An advisory condition: when it is NOT met the step still runs exactly as before, but the card is
    /// flagged <see cref="Tag.NeedsAttention"/> and drawn with a caution scrim so the player notices
    /// that the play, while legal, would achieve nothing on the current board.
    ///
    /// Use it for "the effect is hollow", never for "the effect is illegal" — that is
    /// <see cref="WithCondition"/>. A build card whose only targets are countries the faction already
    /// occupies is the motivating case: CountryState.CanBuild permits the deploy, GameAPI rebuilds in
    /// place, and the board is identical afterwards.
    /// </summary>
    public static T WithAdvisoryCondition<T>(this T step, Func<Condition> getConditionMethod) where T : CardStep
    {
        step.GetAdvisoryConditionsMethod = () => new List<Condition> { getConditionMethod() };
        return step;
    }

    /// <inheritdoc cref="WithAdvisoryCondition"/>
    public static T WithAdvisoryConditions<T>(this T step, Func<List<Condition>> getConditionsMethod) where T : CardStep
    {
        step.GetAdvisoryConditionsMethod = getConditionsMethod;
        return step;
    }

    public static T WithGuidance<T>(this T step, string actionGuidance) where T : CardStep
    {
        step.ActionGuidance = actionGuidance;
        return step;
    }

    /// <summary>
    /// This step is one half of a single action and must not happen on its own: if the step before it
    /// in the card was skipped — its conditions not met, or the player cancelling its selection — this
    /// step is skipped too, quietly, which for a two-step card ends the card.
    ///
    /// Without it, a card that eliminates one of your units and then rebuilds it elsewhere hands out a
    /// free unit whenever the removal half is skipped, because the two steps are otherwise independent.
    ///
    /// **It is also what preserves event ordering across a split**, and that use is newer and less
    /// obvious. When the previous step's event is a trigger, a reaction played in its after-reaction
    /// window calls CardPlayRound.ContinueWithNextSteps, which walks the card pool and re-enters
    /// DoCard for any card with executable steps. Ungated, this step would run INSIDE that window and
    /// put an extra ActivateReactionChangeEvent into the replicated stream, where the single fused
    /// step it was split from emitted its second event strictly after the window closed. The
    /// previous step's StepSucceeded is false for exactly that interval — see
    /// <see cref="CardStep.StepSucceeded"/> — so this gate closes it.
    ///
    /// The cards that deliberately want the opposite (EventBroadFront appends its next battle step
    /// before dispatching the current one, precisely so the window resumes it) must NOT use this.
    /// </summary>
    public static T RequiringPreviousStep<T>(this T step) where T : CardStep
    {
        step.RequiresPreviousStep = true;
        return step;
    }
}
