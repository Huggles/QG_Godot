using System;

/// <summary>
/// Which SOURCES of derived state a change touched, and therefore how much of
/// <see cref="GameStateCalculator.CalculateAll"/> has to run afterwards.
///
/// This exists because the recalculation is the dominant cost of a headless run and the overwhelming
/// majority of it is wasted. Measured over one full game, 93% of ChangeEvents never move a unit, and
/// over half of them are <c>ChangeStepChangeEvent</c> — which advances the turn step and nothing else,
/// yet re-derived supply, attackability and buildability for all six factions every time.
///
/// A scope names what CHANGED, not which phases to run; CalculateAll derives the phases, because the
/// dependency order between them (board -> played cards -> executable steps -> card tags) is its
/// business and not the caller's.
///
/// **The default is <see cref="All"/>, and that is load-bearing.** <c>ChangeEvent.RecalcScope</c>
/// returns All unless an event overrides it, so an unannotated event — including one written years
/// from now by someone who has never read this file — behaves exactly as it does today. Narrowing is
/// opt-in, one small reviewable claim per event. The opposite arrangement, where a central list has to
/// enumerate everything that could possibly invalidate a tag, is impossible to keep correct: 67
/// CustomCondition lambdas in the card scripts read whatever they like.
/// </summary>
[Flags]
public enum RecalcScope
{
    /// <summary>
    /// Nothing derived depends on this. Genuinely rare — <c>ScorePointsChangeEvent</c> is the clear
    /// case, because no condition, tag or card anywhere reads <c>FactionState.Score</c>.
    /// </summary>
    None = 0,

    /// <summary>
    /// Unit positions, country occupancy, strait control or per-turn supply changed: supply,
    /// attackable, buildable and recruitable all have to be re-derived.
    /// </summary>
    Board = 1,

    /// <summary>
    /// A card moved between piles (hand, deck, discard, status, response) or in or out of the play
    /// pool, so which cards count as played changed.
    /// </summary>
    Decks = 2,

    /// <summary>
    /// Turn, round, step, reaction/block trigger context or the per-step play counters changed. Card
    /// conditions read all of these, so step executability and card availability shift even though no
    /// piece moved.
    /// </summary>
    Flow = 4,

    All = Board | Decks | Flow,
}
