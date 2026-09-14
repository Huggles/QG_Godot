using System;
using System.Threading.Tasks;

/// <summary>
/// A step that produces no ChangeEvent because its effect is not a state mutation at all: it
/// registers a modifier that will act later. ResponseRationing and StatusWomenConscripts, both of
/// which register a MutatorRecycleAfterStep and let it do the work once the turn step ends.
///
/// **This is the escape hatch, and it must stay tiny.** If you reach for it and you are actually
/// mutating replicated game state, you want a ChangeEvent and a <see cref="ResultStep"/> — a
/// mutation that does not travel as a ChangeEvent does not reach the other peers, is not in the save
/// stream, and is not in the state hash.
/// </summary>
public sealed partial class EffectStep : CardStep
{
    private readonly Func<Task<CardStepResult>> _effect;

    public EffectStep(CardLogic cardLogic, Func<Task<CardStepResult>> effect) : base(cardLogic)
        => _effect = effect;

    public override StepKind Kind => StepKind.Effect;

    // Inherits RecalcScope.All: a registered modifier is read by conditions (ModifierRegistry feeds
    // ICountryTagModifier and IUnitSupplyModifier among others), and nothing narrower is defensible
    // for an open-ended hook.

    protected override Task<CardStepResult> RunCoreAsync() => _effect();

    protected override Task DispatchAsync(CardStepResult result)
    {
        if (result.Outcome != StepOutcome.Nothing) ReportWrongArm(result);
        return Task.CompletedTask;
    }
}
