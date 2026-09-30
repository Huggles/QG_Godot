using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Prices a forked board for one faction, in victory points relative to the live board — and through
/// that, a single step outcome or a whole card, including the after-reactions each event would open
/// (see <see cref="ReactionForecast"/>).
///
/// The value is the change in the team's lead: score gained now, plus per-turn supply income gained times
/// the rounds left (<see cref="Horizon"/>), minus the same for the enemy team, plus <see cref="CardValue"/>
/// for every card the enemy loses and minus it for every card this team loses. Events are applied to the
/// fork by their own Mutate and the board's tags — supply included — are re-derived after each one.
///
/// Pure with respect to the game: it only reads live state and forks, frees every outcome it builds, and
/// draws nothing.
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

    /// <summary>The forked board's worth to <paramref name="faction"/>'s team, relative to the live board.</summary>
    public static double Value(BoardState board, Faction faction)
    {
        FactionTeam team = StaticGameData.FactionTeamForFaction(faction);
        FactionTeam enemy = StaticGameData.OpponentFactionTeamForFaction(faction);
        int horizon = Horizon;
        return TeamDelta(board, team, horizon) - TeamDelta(board, enemy, horizon);
    }

    private static double TeamDelta(BoardState board, FactionTeam team, int horizon)
    {
        BoardState live = BoardState.Live;
        double score = 0, cards = 0;
        foreach (Faction member in StaticGameData.FactionsForTeam(team))
        {
            if (FactionState.ForEnum(member) == null) continue;
            FactionRecord mine = board.ForFaction(member), now = live.ForFaction(member);
            score += mine.Score - now.Score;
            cards += mine.Hand.Count - now.Hand.Count + mine.Deck.Count - now.Deck.Count;
        }

        int rate = VpMath.SupplyStarVpRate(team, board) - VpMath.SupplyStarVpRate(team, live);
        return score + rate * horizon + cards * CardValue;
    }

    /// <summary>The value of one event on its own, with the reactions it opens; null when it cannot happen.</summary>
    public static double? ValueOf(ChangeEvent changeEvent, Faction faction)
    {
        if (changeEvent == null) return 0;
        List<StepOption> built = new();
        try
        {
            BoardState board = ReactionForecast.Apply(changeEvent, Start(), faction, 0, built);
            return board == null ? null : Value(board, faction);
        }
        finally
        {
            StepChoice.Release(built);
        }
    }

    /// <summary>
    /// The best a card could do if played now: its unfinished steps projected in order, each step taking
    /// its best option given the one before it, reactions included. Null when any step cannot say what it
    /// does — a free-form step, or an outcome no board can take.
    ///
    /// Steps are treated as all running: conditions are not checked, since a later step's conditions read
    /// a board the earlier steps have not changed yet. A step with no options does nothing and hands the
    /// next step no previous outcome. A step that plays another card (Reallocate Resources, Guards) is
    /// worth its best candidate, played out on the board as it stands at that step.
    /// </summary>
    public static double? ValueCard(int cardId, Faction faction)
    {
        CardState state = CardState.ForId(cardId);
        CardLogic card = state?.CardLogic;
        if (card == null) return null;

        List<StepOption> built = new();
        try
        {
            // A Status card from hand is played onto the fork first: its modifier is its standing effect
            // (Scorched Earth cuts supply). The card it spends is added back, as every play spends one.
            if (card.IsStatus && BoardState.Live.ForFaction(state.Faction).Hand.Contains(cardId))
            {
                GameSituation played = PlayCard(cardId, Start(), faction, 0, built);
                return played == null ? null : Value(played.Board, faction) + CardValue;
            }

            GameSituation board = PlaySteps(card.CardSteps.Where(s => !s.StepFinished), Start(), faction, 0, built);
            return board == null ? null : Value(board.Board, faction);
        }
        finally
        {
            StepChoice.Release(built);
        }
    }

    /// <summary>
    /// What a play step would gain by playing <paramref name="cardId"/> now: the card is played onto the
    /// fork and all its steps are projected. For the prompt in which such a step asks which card to take.
    /// </summary>
    public static double? ValuePlay(int cardId, Faction faction)
    {
        if (CardState.ForId(cardId)?.CardLogic == null) return null;

        List<StepOption> built = new();
        try
        {
            GameSituation board = PlayCard(cardId, Start(), faction, 1, built);
            return board == null ? null : Value(board.Board, faction);
        }
        finally
        {
            StepChoice.Release(built);
        }
    }

    /// <summary>How deep one card playing another may nest before the valuer stops following it.</summary>
    private const int MaxPlayDepth = 2;

    /// <summary>A fork of the live board, in the situation the game is in now.</summary>
    private static GameSituation Start() => GameSituation.Live.WithBoard(BoardState.Live.Fork());

    /// <summary>
    /// Project <paramref name="steps"/> onto <paramref name="situation"/>'s board, best option each.
    /// Every option is tried on its own fork; null when a step cannot say what it does.
    /// </summary>
    internal static GameSituation PlaySteps(IEnumerable<CardStep> steps, GameSituation situation, Faction faction,
                                            int depth, List<StepOption> built)
    {
        StepOption? previous = null;
        foreach (CardStep step in steps)
        {
            // No ChangeEvent to project: a registered modifier, or a block of someone else's event.
            if (step.Kind == StepKind.Effect || step.Kind == StepKind.Block) { previous = null; continue; }

            if (step.Kind == StepKind.PlayCard)
            {
                situation = PlayBestCandidate(step, previous, situation, faction, depth, built);
                if (situation == null) return null;
                previous = null;
                continue;
            }

            IReadOnlyList<StepOption> outcomes = step.PossibleOutcomes(new StepContext(situation, previous));
            if (outcomes == null) return null;
            built.AddRange(outcomes);
            if (outcomes.Count == 0) { previous = null; continue; }

            GameSituation best = null;
            StepOption bestOutcome = default;
            double bestValue = double.NegativeInfinity;
            foreach (StepOption outcome in outcomes)
            {
                GameSituation tried = situation.WithBoard(situation.Board.Fork());
                BoardState board = ReactionForecast.Apply(outcome.Event, tried, faction, depth, built);
                if (board == null) continue;
                double value = Value(board, faction);
                if (value > bestValue) { bestValue = value; best = tried.WithBoard(board).After(outcome.Event); bestOutcome = outcome; }
            }

            if (best == null) return null;
            situation = best;
            previous = bestOutcome;
        }
        return situation;
    }

    /// <summary>
    /// A play step: each candidate card is played onto its own fork and its steps are projected — all of
    /// them, since a recycled card is re-armed before it is played. Candidates that cannot be valued are
    /// skipped; null only when none can.
    /// </summary>
    private static GameSituation PlayBestCandidate(CardStep step, StepOption? previous, GameSituation situation,
                                                   Faction faction, int depth, List<StepOption> built)
    {
        if (depth >= MaxPlayDepth) return null;

        IReadOnlyList<int> candidates;
        try { candidates = step.PossiblePlays(new StepContext(situation, previous)); }
        catch (Exception) { return null; }
        if (candidates == null) return null;
        if (candidates.Count == 0) return situation;

        GameSituation best = null;
        double bestValue = double.NegativeInfinity;
        foreach (int candidate in candidates)
        {
            GameSituation tried = PlayCard(candidate, situation.WithBoard(situation.Board.Fork()), faction, depth + 1, built);
            if (tried == null) continue;

            double value = Value(tried.Board, faction);
            if (value > bestValue) { bestValue = value; best = tried; }
        }
        return best;
    }

    /// <summary>The card leaves its pile onto <paramref name="situation"/>'s board, then its steps are projected.</summary>
    private static GameSituation PlayCard(int cardId, GameSituation situation, Faction faction, int depth, List<StepOption> built)
    {
        CardLogic logic = CardState.ForId(cardId)?.CardLogic;
        if (logic == null) return null;

        PlayCardChangeEvent play = new(cardId) { IsTrigger = false };
        built.Add(new StepOption(null, play));
        if (!situation.Board.Apply(play, situation, cardTags: false)) return null;

        return PlaySteps(logic.CardSteps, situation.After(play), faction, depth, built);
    }
}
