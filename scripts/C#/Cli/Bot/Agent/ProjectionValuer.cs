using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Prices a forked board for one faction, in victory points relative to the live board — and through
/// that, a single step outcome or a whole card, including the after-reactions each event would open
/// (see <see cref="ReactionForecast"/>).
///
/// The value is the change in the team's lead: score gained now, plus per-turn income gained times the
/// rounds left (<see cref="Horizon"/>), minus the same for the enemy team, plus <see cref="CardValue"/>
/// for every card the enemy loses and minus it for every card this team loses. Income is supply stars
/// plus Status-card VP; <c>bot_value_status=none|AXIS|ALLIES</c> drops the Status half for an A/B
/// (it measured +17pp Axis / +6pp Allies on 2026-10-04). Events are applied to the
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

    /// <summary>
    /// What one card is worth to <paramref name="faction"/> right now. <see cref="CardValue"/> unless
    /// <c>bot_card_value=all|none|AXIS|ALLIES</c> includes it; then a card is worth what an average play
    /// from its own hand is worth this turn — the mean of each hand card's single-play value
    /// (<see cref="ValueCard"/>, negatives and unvalued cards counting 0), never less than
    /// <see cref="CardValue"/>.
    ///
    /// A fixed price drifts away from everything else: once pieces and reach make a Build worth more,
    /// five cards for one Build (Reallocate Resources) look ever cheaper. Priced by the plays they could
    /// have been, cards cost what the same valuation says a play is worth.
    ///
    /// Measured once while the board stands still (the turn, the faction asking, its hand and its deck),
    /// at the flat price: the hand's own valuations must not ask what a card is worth while they run.
    /// </summary>
    public static double CardWorth(Faction faction)
    {
        if (_pricing || !BotSides.Includes("bot_card_value", "none", faction)) return CardValue;

        BoardState live = BoardState.Live;
        FactionRecord own = live.ForFaction(faction);
        var stamp = (live.GameTurn, string.Join(",", own.Hand), own.Deck.Count);
        if (Worth.TryGetValue(faction, out var known) && known.Stamp == stamp) return known.Worth;

        double worth;
        _pricing = true;
        try
        {
            List<int> hand = own.Hand.ToList();
            double total = hand.Sum(id => Math.Max(ValueCard(id, faction) ?? 0, 0));
            worth = hand.Count == 0 ? CardValue : Math.Max(total / hand.Count, CardValue);
        }
        finally
        {
            _pricing = false;
        }
        Worth[faction] = (stamp, worth);
        if (CliArgs.Verbose) DebugUtilities.PrintPeer($"CARDWORTH {faction} = {worth:0.##} over {own.Hand.Count} hand card(s)");
        return worth;
    }

    private static bool _pricing;
    private static readonly Dictionary<Faction, ((int, string, int) Stamp, double Worth)> Worth = new();

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
    public static double Value(BoardState board, Faction faction) => Value(board, faction, Horizon);

    /// <summary>
    /// <see cref="Value(BoardState, Faction)"/> with income counted over <paramref name="horizon"/>
    /// rounds — fewer for a board reached on a later turn (see <see cref="TurnLookahead"/>).
    /// </summary>
    public static double Value(BoardState board, Faction faction, int horizon)
    {
        FactionTeam team = StaticGameData.FactionTeamForFaction(faction);
        FactionTeam enemy = StaticGameData.OpponentFactionTeamForFaction(faction);
        Terms terms = new(CardWorth(faction), BotSides.Includes("bot_value_status", "all", faction),
                          BotSides.Includes("bot_material", "none", faction) ? MaterialVp : 0,
                          BotSides.Includes("bot_reach", "none", faction) ? ReachWeight : 0,
                          BotSides.Includes("bot_scarcity", "none", faction));
        return TeamDelta(board, team, horizon, terms) - TeamDelta(board, enemy, horizon, terms);
    }

    /// <summary>Which optional terms a valuation counts, and at what weight; 0 leaves a term out.</summary>
    private readonly record struct Terms(double Card, bool Status, double Material, double Reach, bool Scarcity);

    /// <summary>
    /// How many of <paramref name="faction"/>'s remaining turns its cards cannot cover on
    /// <paramref name="board"/>: each turn spends at least the card it plays, and hand and deck are all
    /// it has left to play. For <c>bot_scarcity=all|none|AXIS|ALLIES</c>, each such turn costs what a play
    /// is worth (<see cref="CardWorth"/>) and the VP a turn with an empty hand forfeits — the price of
    /// burning cards (four for Reallocate Resources) once the deck can no longer last the game. While
    /// the deck can, cards above the need cost only their own worth.
    /// </summary>
    private static int Shortfall(BoardState board, Faction faction, int horizon)
    {
        FactionRecord piles = board.ForFaction(faction);
        return Math.Max(horizon - piles.Hand.Count - piles.Deck.Count, 0);
    }

    /// <summary>
    /// What one piece on the board is worth in VP for <c>bot_material=all|none|AXIS|ALLIES</c>, set by
    /// <c>bot_material_vp</c> (1 by default): the card and the turn it takes to build it again. Without
    /// it a piece that earns nothing this turn is worth nothing, so a Navy holding the sea lanes can be
    /// sunk for free and a card that saves one (Radar) can never be worth its cost.
    /// </summary>
    public static double MaterialVp => CliArgs.GetDouble("bot_material_vp", 1.0);

    /// <summary>
    /// <c>bot_material_mode=front</c>: a piece next to any space an enemy holds is worth all of
    /// <see cref="MaterialVp"/>, a piece in the rear half of it. A piece on the front line is the one a battle takes and the one that
    /// opens battles; a piece in the rear can be rebuilt at leisure. <c>flat</c>, the default, values
    /// every piece the same.
    /// </summary>
    private static bool FrontMaterial => CliArgs.Get("bot_material_mode", "flat") == "front";

    /// <summary>What <paramref name="faction"/>'s pieces on <paramref name="board"/> are worth, in units of <see cref="MaterialVp"/>.</summary>
    private static double Material(BoardState board, Faction faction)
    {
        List<int> units = board.ActiveUnitIds(faction);
        if (!FrontMaterial) return units.Count;

        FactionTeam enemy = StaticGameData.OpponentFactionTeamForFaction(faction);
        double total = 0;
        foreach (int unitId in units)
        {
            CountryState at = board.CountryStateOf(UnitState.ForId(unitId));
            if (at == null) continue;
            bool front = board.AdjacentCountryIds(faction, at)
                .Any(id => CountryState.ForId(id) is { } neighbour
                           && board.UnitsIn(neighbour).Keys.Any(f => StaticGameData.FactionTeamForFaction(f) == enemy));
            total += front ? 1.0 : 0.5;
        }
        return total;
    }

    /// <summary>
    /// How much of the income a faction's best next build would add counts as income already, for
    /// <c>bot_reach=all|none|AXIS|ALLIES</c>, set by <c>bot_reach_weight</c> (0.5 by default) — see
    /// <see cref="VpMath.BestBuildVp"/>. Pieces near income they could take are worth that much more
    /// than the same pieces out of reach.
    /// </summary>
    public static double ReachWeight => CliArgs.GetDouble("bot_reach_weight", 0.5);

    private static double TeamDelta(BoardState board, FactionTeam team, int horizon, Terms terms)
    {
        BoardState live = BoardState.Live;
        double score = 0, cards = 0, units = 0, reach = 0, shortfall = 0;
        foreach (Faction member in StaticGameData.FactionsForTeam(team))
        {
            if (FactionState.ForEnum(member) == null) continue;
            FactionRecord mine = board.ForFaction(member), now = live.ForFaction(member);
            score += mine.Score - now.Score - (mine.ProjectedCharge - now.ProjectedCharge);
            cards += mine.Hand.Count - now.Hand.Count + mine.Deck.Count - now.Deck.Count;
            if (terms.Material != 0) units += Material(board, member) - Material(live, member);
            if (terms.Reach != 0) reach += VpMath.BestBuildVp(member, board) - VpMath.BestBuildVp(member, live);
            if (terms.Scarcity) shortfall += Shortfall(board, member, horizon) - Shortfall(live, member, horizon);
        }

        int rate = VpMath.SupplyStarVpRate(team, board) - VpMath.SupplyStarVpRate(team, live);
        if (terms.Status) rate += VpMath.StatusCardVpRateOn(team, board) - VpMath.StatusCardVpRateOn(team, live);
        return score + (rate + reach * terms.Reach) * horizon + cards * terms.Card + units * terms.Material
               - shortfall * (terms.Card + 1);
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
            // The card a Status play spends is added back, as every play spends one.
            bool spends = card.IsStatus && BoardState.Live.ForFaction(state.Faction).Hand.Contains(cardId);
            GameSituation board = ProjectCard(cardId, Start(), faction, false, built);
            return board == null ? null : Value(board.Board, faction) + (spends ? CardWorth(faction) : 0);
        }
        finally
        {
            StepChoice.Release(built);
        }
    }

    /// <summary>
    /// <paramref name="start"/>, a fork, as it would be once <paramref name="cardId"/> is played or
    /// activated: null when the card cannot be valued (see <see cref="ValueCard"/>).
    ///
    /// A Status card from hand is played onto the fork first: its modifier is its standing effect
    /// (Scorched Earth cuts supply). With <paramref name="spendFromHand"/> — a line of several turns,
    /// which must not play the same card twice — every card in hand leaves it, and a Status or
    /// Response card is only put on the table: its activations are the later turns' reactions, not an
    /// imagined one now. Otherwise only the steps of a card in hand are projected, as a single-play
    /// valuation always has.
    /// </summary>
    internal static GameSituation ProjectCard(int cardId, GameSituation start, Faction faction, bool spendFromHand,
                                              List<StepOption> built)
    {
        CardState state = CardState.ForId(cardId);
        CardLogic card = state?.CardLogic;
        if (card == null) return null;

        bool inHand = start.Board.ForFaction(state.Faction).Hand.Contains(cardId);
        if (inHand && spendFromHand)
            return PlayCard(cardId, start, faction, 0, built, steps: !(card.IsStatus || card.IsResponse));
        if (inHand && card.IsStatus)
            return PlayCard(cardId, start, faction, 0, built);

        return PlaySteps(Unfinished(card, start), start, faction, 0, built);
    }

    /// <summary>A card's steps still to run: all of them on a re-armed later turn.</summary>
    internal static IEnumerable<CardStep> Unfinished(CardLogic card, GameSituation situation)
        => card.CardSteps.Where(s => situation.Rearmed || !s.StepFinished);

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
    internal static GameSituation Start() => GameSituation.Live.WithBoard(BoardState.Live.Fork());

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
        double before = Value(situation.Board, faction);
        foreach (int candidate in candidates)
        {
            GameSituation tried = PlayCard(candidate, situation.WithBoard(situation.Board.Fork()), faction, depth + 1, built);
            if (tried == null) continue;
            ChargeDeckTempo(candidate, situation.Board, tried.Board, faction, before);

            double value = Value(tried.Board, faction);
            if (value > bestValue) { bestValue = value; best = tried; }
        }
        return best;
    }

    /// <summary>
    /// For <c>bot_deck_tempo=all|none|AXIS|ALLIES</c>: a card played straight out of its deck
    /// (Reallocate Resources) would have been drawn and played later anyway, so what it gains now is
    /// mostly borrowed from that later turn. It is about half the deck away, at a card a turn; of what
    /// it gains, the part it would still have earned after that wait is charged to its faction on
    /// <paramref name="after"/>: <c>gain × max(0, 1 − (deck ÷ 2) ÷ rounds left)</c>. A deck deeper
    /// than the game would never have reached it, and is charged nothing. Pulling a card early pays for
    /// the turns it saves, not for the card.
    /// </summary>
    private static void ChargeDeckTempo(int cardId, BoardState before, BoardState after, Faction faction, double valueBefore)
    {
        if (!BotSides.Includes("bot_deck_tempo", "none", faction)) return;
        CardState card = CardState.ForId(cardId);
        if (card == null) return;
        // The gain is the valuer's: only a play by its own team is a loan against its own deck.
        if (StaticGameData.FactionTeamForFaction(card.Faction) != StaticGameData.FactionTeamForFaction(faction)) return;
        FactionRecord owner = before.ForFaction(card.Faction);
        if (!owner.Deck.Contains(cardId)) return;

        double gain = Value(after, faction) - valueBefore;
        if (gain <= 0) return;
        double wait = owner.Deck.Count / 2.0;
        double share = Math.Max(0, 1 - wait / Horizon);
        double charge = gain * share;
        if (charge <= 0) return;

        after.ForFaction(card.Faction).ProjectedCharge += charge;
        if (CliArgs.Verbose)
            DebugUtilities.PrintPeer($"DECKTEMPO {card.CardName} ({card.Faction}) gain={gain:0.##} deck={owner.Deck.Count} share={share:0.##} charge={charge:0.##}");
    }

    /// <summary>The card leaves its pile onto <paramref name="situation"/>'s board, then its steps are projected.</summary>
    private static GameSituation PlayCard(int cardId, GameSituation situation, Faction faction, int depth, List<StepOption> built,
                                          bool steps = true)
    {
        CardLogic logic = CardState.ForId(cardId)?.CardLogic;
        if (logic == null) return null;

        PlayCardChangeEvent play = new(cardId) { IsTrigger = false };
        built.Add(new StepOption(null, play));
        if (!situation.Board.Apply(play, situation, cardTags: false)) return null;

        return steps ? PlaySteps(logic.CardSteps, situation.After(play), faction, depth, built) : situation.After(play);
    }
}
