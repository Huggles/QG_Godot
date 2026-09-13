using System.Collections.Generic;

/// <summary>
/// Turns one kind of board fact into goals.
///
/// Proposers are **stateless singletons**, held in a fixed list and run in that order — the same
/// discipline <see cref="BotRuleRegistry"/> imposes on rules, and for the same two reasons. State on a
/// proposer would be state the agenda cannot see and the trace cannot explain; and the value sums the
/// agenda builds are floating point, so a changed iteration order changes the numbers.
///
/// The three prohibitions from <see cref="IBotRule"/> apply here too and are, if anything, tighter:
///
///  1. **No randomness.** Not a <c>System.Random</c>, and never <c>GameRandom</c>. The agenda must be a
///     pure function of the board, or the same (seed, decision_seed) stops reproducing.
///  2. **No mutation.** Read state; change none of it. No ChangeEvent, no CalculateAll.
///  3. **No legality claims.** A goal says "this would be worth having", never "this move is available".
///     The offer set on a prompt remains the only authority on what can actually be done.
///
/// Proposers append into a caller-owned list rather than returning one: a proposer that finds nothing is
/// the common case, and it should cost no allocation.
/// </summary>
public interface IGoalProposer
{
    /// <summary>Which kind this proposer emits. Used for weighting and for per-kind trace counters.</summary>
    GoalKind Kind { get; }

    /// <summary>
    /// Append goals for <paramref name="assessment"/> into <paramref name="into"/>, already scaled by
    /// <paramref name="weight"/> and by the horizon. Must not read anything outside the assessment and
    /// live board state, and must produce the same output for the same board every time.
    /// </summary>
    void Propose(BoardAssessment assessment, double weight, List<Goal> into);
}
