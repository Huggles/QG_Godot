using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Prices a projected board for one faction, in victory points relative to the live board — and
/// through that, a single step outcome or a whole card.
///
/// The value is the change in the team's lead: score gained now, plus per-turn income gained times the
/// rounds left (<see cref="Horizon"/>), minus the same for the enemy team, plus <see cref="CardValue"/>
/// for every card the enemy loses and minus it for every card this team loses. Everything else a
/// card does — supply, tags, reactions — is not modelled, so this is a floor on what a play is worth,
/// not the whole of it.
///
/// Pure: it only reads live state and projections, frees every outcome it builds, and draws nothing.
/// </summary>
public static class ProjectionValuer
{
    /// <summary>
    /// What one card in hand or deck is worth in VP. The one number here that is a guess rather than
    /// the scoring rule: without it every discard is free, and a card that pays two cards to battle
    /// looks better than one that pays nothing. Measure before trusting.
    /// </summary>
    public const double CardValue = 1.0;

    /// <summary>Rounds still to score, floored at 1 so a last-round income change still counts.</summary>
    public static int Horizon
    {
        get
        {
            GameFlow flow = GameFlow.Instance;
            return flow == null ? 1 : Math.Max(flow.MaxRound - flow.Round, 1);
        }
    }

    /// <summary>The projected board's worth to <paramref name="faction"/>'s team, relative to the live board.</summary>
    public static double Value(BoardProjection projection, Faction faction)
    {
        FactionTeam team = StaticGameData.FactionTeamForFaction(faction);
        FactionTeam enemy = StaticGameData.OpponentFactionTeamForFaction(faction);
        int horizon = Horizon;

        double mine = TeamDelta(projection, team, horizon);
        double theirs = TeamDelta(projection, enemy, horizon);
        return mine - theirs;
    }

    private static double TeamDelta(BoardProjection projection, FactionTeam team, int horizon)
    {
        double score = 0, cards = 0;
        foreach (Faction member in StaticGameData.FactionsForTeam(team))
        {
            if (FactionState.ForEnum(member) == null) continue;
            score += projection.ScoreOf(member) - FactionState.ForEnum(member).Score;
            DeckState deck = DeckState.ForFaction(member);
            cards += projection.HandCount(member) - deck.HandCardIds.Count
                   + projection.DeckCount(member) - deck.DeckCardIds.Count;
        }

        int rate = VpMath.SupplyStarVpRate(team, projection) - VpMath.SupplyStarVpRate(team);
        return score + rate * horizon + cards * CardValue;
    }

    /// <summary>The value of one event on its own, or null when it cannot be projected.</summary>
    public static double? ValueOf(ChangeEvent changeEvent, Faction faction)
    {
        if (changeEvent == null) return 0;
        BoardProjection projection = BoardProjection.FromLive();
        return projection.Apply(changeEvent) ? Value(projection, faction) : null;
    }

    /// <summary>
    /// The best a card could do if played now: its unfinished steps projected in order, each step
    /// taking its best option given the one before it. Null when any step cannot say what it does —
    /// a free-form step, an event the projection does not model.
    ///
    /// Steps are treated as all running: conditions are not checked, since a later step's conditions
    /// read a board the earlier steps have not changed yet. A step with no options does nothing and
    /// hands the next step no previous outcome. A step that plays another card (Reallocate Resources,
    /// Guards) is worth its best candidate, played out on the board as it stands at that step.
    /// </summary>
    public static double? ValueCard(int cardId, Faction faction)
    {
        CardLogic card = CardState.ForId(cardId)?.CardLogic;
        if (card == null) return null;

        List<StepOption> built = new();
        try
        {
            BoardProjection board = PlaySteps(card.CardSteps.Where(s => !s.StepFinished), BoardProjection.FromLive(), faction, 0, built);
            return board == null ? null : Value(board, faction);
        }
        finally
        {
            StepChoice.Release(built);
        }
    }

    /// <summary>
    /// What a play step would gain by playing <paramref name="cardId"/> now: the card leaves its pile
    /// and all its steps are projected. For the prompt in which such a step asks which card to take.
    /// </summary>
    public static double? ValuePlay(int cardId, Faction faction)
    {
        CardLogic card = CardState.ForId(cardId)?.CardLogic;
        if (card == null) return null;

        List<StepOption> built = new();
        try
        {
            BoardProjection board = BoardProjection.FromLive();
            board.PlayCard(cardId);
            if (board.IsUnknown) return null;
            board = PlaySteps(card.CardSteps, board, faction, 1, built);
            return board == null ? null : Value(board, faction);
        }
        finally
        {
            StepChoice.Release(built);
        }
    }

    /// <summary>How deep one card playing another may nest before the valuer stops following it.</summary>
    private const int MaxPlayDepth = 2;

    /// <summary>Project <paramref name="steps"/> onto <paramref name="board"/>, best option each; null when unknown.</summary>
    private static BoardProjection PlaySteps(IEnumerable<CardStep> steps, BoardProjection board, Faction faction,
                                             int depth, List<StepOption> built)
    {
        StepOption? previous = null;
        foreach (CardStep step in steps)
        {
            // No ChangeEvent to project: a registered modifier, or a block of someone else's event.
            if (step.Kind == StepKind.Effect || step.Kind == StepKind.Block) { previous = null; continue; }

            if (step.Kind == StepKind.PlayCard)
            {
                board = PlayBestCandidate(step, previous, board, faction, depth, built);
                if (board == null) return null;
                previous = null;
                continue;
            }

            IReadOnlyList<StepOption> outcomes = step.PossibleOutcomes(StepContext.Live(previous));
            if (outcomes == null) return null;
            built.AddRange(outcomes);
            if (outcomes.Count == 0) { previous = null; continue; }

            BoardProjection bestBoard = null;
            StepOption best = default;
            double bestValue = double.NegativeInfinity;
            foreach (StepOption outcome in outcomes)
            {
                BoardProjection tried = board.Fork();
                if (!tried.Apply(outcome.Event)) continue;
                double value = Value(tried, faction);
                if (value > bestValue) { bestValue = value; bestBoard = tried; best = outcome; }
            }

            if (bestBoard == null) return null;
            board = bestBoard;
            previous = best;
        }
        return board;
    }

    /// <summary>
    /// A play step: each candidate card leaves its pile as DeckState.PlayCard would move it, then its
    /// own steps are projected — all of them, since a recycled card is re-armed before it is played.
    /// Candidates that cannot be valued are skipped; null only when none can.
    /// </summary>
    private static BoardProjection PlayBestCandidate(CardStep step, StepOption? previous, BoardProjection board,
                                                     Faction faction, int depth, List<StepOption> built)
    {
        if (depth >= MaxPlayDepth) return null;

        IReadOnlyList<int> candidates;
        try { candidates = step.PossiblePlays(StepContext.Live(previous)); }
        catch (Exception) { return null; }
        if (candidates == null) return null;
        if (candidates.Count == 0) return board;

        BoardProjection bestBoard = null;
        double bestValue = double.NegativeInfinity;
        foreach (int candidate in candidates)
        {
            CardLogic logic = CardState.ForId(candidate)?.CardLogic;
            if (logic == null) continue;

            BoardProjection tried = board.Fork();
            tried.PlayCard(candidate);
            if (tried.IsUnknown) continue;

            tried = PlaySteps(logic.CardSteps, tried, faction, depth + 1, built);
            if (tried == null) continue;

            double value = Value(tried, faction);
            if (value > bestValue) { bestValue = value; bestBoard = tried; }
        }
        return bestBoard;
    }
}
