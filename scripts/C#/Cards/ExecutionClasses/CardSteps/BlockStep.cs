using System;
using System.Threading.Tasks;

/// <summary>
/// A block reaction: the twelve "do not remove my X" / "that card does nothing" cards. It produces
/// no ChangeEvent of its own — its whole effect is to mark the event this activation was OFFERED to
/// block, which <c>CardPlayRound.DoChangeEvent</c> then declines to apply.
///
/// <typeparamref name="TTrigger"/> is the event this card blocks, and the type parameter does real
/// work: every block card used to open with
/// <c>if (ActivationTrigger is not RemoveUnitChangeEvent e) return;</c>, hand-rolled twelve times.
/// The cast happens once, here, and a card offered against an event it does not understand logs the
/// mismatch instead of no-oping silently — or, in ResponseASWTactics' case, dereferencing a null
/// ActivationTrigger.
///
/// Read the trigger from <see cref="CardLogic.ActivationTrigger"/>, never from
/// CardPlayPool.LastNoneNewCardChangeEvent: that is a live pool read, and every ChangeEvent.Apply()
/// self-registers into the pool, so a cost applied by a RequirementStep earlier in the same card
/// would silently retarget it. That is exactly what made StatusRadar discard its two cards and then
/// fail to block the Navy removal.
/// </summary>
public sealed partial class BlockStep<TTrigger> : CardStep where TTrigger : ChangeEvent
{
    private readonly Func<TTrigger, Task<CardStepResult>> _block;

    public BlockStep(CardLogic cardLogic, Func<TTrigger, Task<CardStepResult>> block) : base(cardLogic)
        => _block = block;

    public override StepKind Kind => StepKind.Block;

    /// <summary>
    /// Board and Flow, and this is the step kind that proves the scope declaration earns its keep:
    /// a block mutates state OUTSIDE any ChangeEvent, so nothing else recalculates for it. Board
    /// because it sets UnitState.ImmuneForTurn, which is in MultiplayerGameState.ComputeHash and
    /// feeds Tag.Attackable; Flow because it sets CardState.IsBlocked, which CanBeActivated reads.
    /// Decks is the one source a block genuinely cannot move.
    /// </summary>
    public override RecalcScope RecalcScope => RecalcScope.Board | RecalcScope.Flow;

    protected override async Task<CardStepResult> RunCoreAsync()
    {
        if (CardLogic.ActivationTrigger is not TTrigger trigger)
        {
            DebugUtilities.PrintPeerError(
                $"{CardLogic?.CardState?.CardName} #{Id}: block step expected {typeof(TTrigger).Name}, " +
                $"got {CardLogic?.ActivationTrigger?.GetType().Name ?? "null"}");
            return CardStepResult.Nothing;
        }
        return await _block(trigger);
    }

    protected override Task DispatchAsync(CardStepResult result)
    {
        // The trigger is re-resolved rather than captured from RunCoreAsync: nothing between the two
        // can change it (DispatchAsync is called with the result, in the same await chain), and
        // re-reading keeps the "always read ActivationTrigger" rule true of every line in this file.
        if (CardLogic.ActivationTrigger is not { } trigger) return Task.CompletedTask;

        switch (result.Outcome)
        {
            case StepOutcome.Nothing:
                break;

            case StepOutcome.BlockCard:
                // Block the whole card, so its remaining steps do not run — CardPlayRound.DoCard's
                // loop reads CardLogic.IsBlocked. ResponseASWTactics is the only base-game user.
                trigger.IsCardBlocked = true;
                trigger.IsBlocked = true;
                break;

            case StepOutcome.Block:
                trigger.IsBlocked = true;
                break;

            default:
                ReportWrongArm(result);
                break;
        }
        return Task.CompletedTask;
    }
}
