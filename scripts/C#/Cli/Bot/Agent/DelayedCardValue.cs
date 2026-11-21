using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// What a Status or Response card in hand is worth beyond the turn it is put down, which a projection of
/// the play alone sees as 0. Three parts, all counted for the rounds still to come:
///
///  - own turns: each card in the faction's hand and deck (a Response: its hand only) is played at the start
///    of its next turn on two forks, with the card on the table and without; the difference is what one
///    firing adds (Blitzkrieg's Army after a land battle). A play-step activation (Conscription) counts too.
///  - enemy turns: the few events an enemy turn could bring that the card might answer (Radar saving a
///    Navy, Stalingrad an Army, Rasputitsa removing a new Army), each played out with the card and without.
///  - card quality: the four cards that change which cards are drawn (Rosie the Riveter, Women Conscripts,
///    Superior Planning, Rationing), priced by how much better those cards are than an average draw.
///
/// A Status card fires once per turn; a Response once. A firing's income is counted over half the horizon,
/// as it comes on average halfway through it, and the total is discounted by <c>bot_delayed_discount</c>
/// (0.5): a sure gain now beats a forecast one of the same size. <c>bot_delayed_cards</c> turns it on.
/// </summary>
public static class DelayedCardValue
{
    public static bool Applies(Faction faction) => BotSides.Includes("bot_delayed_cards", "none", faction);

    public static double Discount => CliArgs.GetDouble("bot_delayed_discount", 0.5);

    /// <summary>Rounds a card's measured value is reused for, scaled to the rounds left; measuring costs ~0.2 s a card.</summary>
    public static int Refresh => Math.Max(CliArgs.GetInt("bot_delayed_refresh", 2), 1);

    private static readonly Dictionary<int, (int Round, int Horizon, double Value)> Known = new();
    private static readonly Dictionary<Faction, double[]> DeckValues = new();
    private static int _turn = -1;

    /// <summary>Drop every cached value; the valuation switches they were measured under may have changed.</summary>
    public static void Forget()
    {
        Known.Clear();
        _turn = -1;
        NewTurn();
    }

    private static int Round => GameFlow.Instance?.Round ?? 0;

    /// <summary>
    /// What keeping <paramref name="cardId"/> is worth: its value if played now, an average card's when
    /// that cannot be computed, plus its delayed value for a Status or Response card in hand.
    /// </summary>
    public static double KeepValue(int cardId, Faction faction)
    {
        CardState card = CardState.ForId(cardId);
        if (card?.CardLogic == null) return ProjectionValuer.CardWorth(faction);
        bool table = card.CardLogic.IsStatus || card.CardLogic.IsResponse;
        bool inHand = BoardState.Live.ForFaction(card.Faction).Hand.Contains(cardId);
        if (table && !inHand) return ProjectionValuer.CardWorth(faction);

        NewTurn();
        if (!PlayValues.TryGetValue(cardId, out double? now)) PlayValues[cardId] = now = ProjectionValuer.ValueCard(cardId, faction);
        double value = now ?? ProjectionValuer.CardWorth(faction);
        return table && Applies(faction) ? value + Of(cardId, faction) : value;
    }

    /// <summary>
    /// Remember a hand card's play value from this turn's card choice, so ranking the hand for a discard
    /// later in the turn does not project every card again.
    /// </summary>
    public static void NotePlayValue(int cardId, double? value)
    {
        NewTurn();
        PlayValues[cardId] = value;
    }

    private static readonly Dictionary<int, double?> PlayValues = new();

    /// <summary>The discounted delayed value of a Status or Response card in the faction's hand; 0 for any other card.</summary>
    public static double Of(int cardId, Faction faction)
    {
        CardState card = CardState.ForId(cardId);
        if (card?.CardLogic == null || !(card.CardLogic.IsStatus || card.CardLogic.IsResponse)) return 0;
        if (!BoardState.Live.ForFaction(faction).Hand.Contains(cardId)) return 0;

        int horizon = ProjectionValuer.Horizon;
        if (Known.TryGetValue(cardId, out var known) && Round - known.Round < Refresh)
            return known.Value * horizon / Math.Max(known.Horizon, 1);
        bool once = card.CardLogic.IsResponse;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        double? quality = Quality(card, faction, horizon);
        double own = quality ?? OwnTurns(card, faction, horizon, once);
        long ownMs = clock.ElapsedMilliseconds;
        double enemy = EnemyTurns(card, faction, horizon, once);
        long enemyMs = clock.ElapsedMilliseconds - ownMs;
        double value = Discount * (Math.Max(own, 0) + Math.Max(enemy, 0));

        if (CliArgs.Verbose)
            DebugUtilities.PrintPeer($"DELAYED {faction} {card.CardName} = {value:0.##} (own {own:0.##}, enemy {enemy:0.##}{(quality != null ? ", quality" : "")}) in {ownMs}+{enemyMs} ms");
        Known[cardId] = (Round, horizon, value);
        return value;
    }

    /// <summary>Keeps the deck worths and the enemy-turn boards without the card for the turn they were measured in.</summary>
    private static void NewTurn()
    {
        int turn = BoardState.Live.GameTurn;
        if (turn == _turn) return;
        DeckValues.Clear();
        PlayValues.Clear();
        WithoutTurns.Clear();
        WithoutPlays.Clear();
        StepChoice.Release(WithoutBuilt);
        WithoutBuilt.Clear();
        _turn = turn;
    }

    /// <summary>
    /// What the card adds to the faction's own turns: each card of its bag played next turn with the card on
    /// the table, against without, and the card's own play-step activation against the play it replaces.
    /// </summary>
    private static double OwnTurns(CardState card, Faction faction, int horizon, bool once)
    {
        List<StepOption> built = new();
        try
        {
            BoardState with = BoardState.Live.Fork();
            PlayCardChangeEvent put = new(card.Id) { IsTrigger = false };
            built.Add(new StepOption(null, put));
            if (!with.Apply(put, GameSituation.Live.WithBoard(with), cardTags: false)) return 0;

            BoardState a = TurnLookahead.TurnOf(with, faction, built), b = WithoutTurn(faction);
            if (a == null || b == null) return 0;
            double stand = Firing(a, b, faction, horizon);

            // The faction picks its plays, so of its triggering cards, as many get played as it has turns
            // for: copies × min(1, turns ÷ bag). A Response fires once, with the best card in hand.
            FactionRecord bag = with.ForFaction(faction);
            List<CardState> cards = CardState.ForIds((once ? bag.Hand : bag.Hand.Concat(bag.Deck)).ToList())
                .Where(c => c.CardLogic != null && !c.CardLogic.IsStatus && !c.CardLogic.IsResponse).ToList();
            double sum = 0, best = 0;
            int triggers = 0;
            foreach (IGrouping<string, CardState> copies in cards.GroupBy(c => c.CardName))
            {
                int id = copies.First().Id;
                string key = $"own {faction} {copies.Key}";
                if (!WithoutPlays.TryGetValue(key, out BoardState tb)) WithoutPlays[key] = tb = Project(id, b, faction, WithoutBuilt)?.Board;
                GameSituation ta = Project(id, a, faction, built);
                if (ta == null || tb == null) continue;
                double gain = Firing(ta.Board, tb, faction, horizon) - stand;
                if (CliArgs.Verbose && Math.Abs(gain) > 1e-9) DebugUtilities.PrintPeer($"OWNTURNS {card.CardName} via {copies.Key} x{copies.Count()} {gain:0.##}");
                if (gain <= 1e-9) continue;
                triggers += copies.Count();
                sum += copies.Count() * gain;
                best = Math.Max(best, gain);
            }

            double plays = cards.Count == 0 ? 0 : Math.Min(1.0, horizon / (double)cards.Count);
            double value = once ? best : sum * plays;
            return value + Activation(card, a, faction, horizon, once, built);
        }
        finally
        {
            StepChoice.Release(built);
        }
    }

    private static readonly Dictionary<Faction, BoardState> WithoutTurns = new();
    private static readonly Dictionary<string, BoardState> WithoutPlays = new();
    private static readonly List<StepOption> WithoutBuilt = new();

    /// <summary>
    /// What the card is worth on enemy turns, without replaying every enemy card: the events an enemy turn
    /// could bring against our team are listed (a battle on each of our pieces in its reach, a build in each
    /// space it can build in, each of its EW cards) and each is played out, blocks and reactions included,
    /// with the card on the table and without. The card's own triggers decide which it answers.
    /// </summary>
    private static double EnemyTurns(CardState card, Faction faction, int horizon, bool once)
    {
        List<StepOption> built = new();
        try
        {
            BoardState with = BoardState.Live.Fork();
            PlayCardChangeEvent put = new(card.Id) { IsTrigger = false };
            built.Add(new StepOption(null, put));
            if (!with.Apply(put, GameSituation.Live.WithBoard(with), cardTags: false)) return 0;

            // Firings expected per round (each threat weighted by its chance an enemy turn brings it) and their worth.
            double rate = 0, sum = 0;
            foreach (Faction enemy in StaticGameData.FactionsForTeam(StaticGameData.OpponentFactionTeamForFaction(faction)))
            {
                if (FactionState.ForEnum(enemy) == null) continue;
                BoardState a = TurnLookahead.TurnOf(with, enemy, built), b = WithoutTurn(enemy);
                if (a == null || b == null) continue;
                double stand = Firing(a, b, faction, horizon);

                // EW is played out only for a card that could change it, and only until one EW card shows it does not.
                bool ewMatters = card.CardLogic is IDiscardModifier || card.CardLogic.HasEventBasedTrigger;
                foreach ((string key, double chance, ChangeEvent probe, Func<BoardState, List<StepOption>, BoardState> play) in Threats(a, enemy, faction))
                {
                    bool answers = probe == null ? ewMatters : chance > 0 && MightAnswer(card, probe, a);
                    probe?.Free();
                    if (chance <= 0 || !answers) continue;
                    if (!WithoutPlays.TryGetValue(key, out BoardState tb)) WithoutPlays[key] = tb = play(b, WithoutBuilt);
                    BoardState ta = play(a, built);
                    if (ta == null || tb == null) continue;
                    double gain = Firing(ta, tb, faction, horizon) - stand;
                    if (probe == null && gain <= 1e-9) ewMatters = false;
                    if (gain <= 1e-9) continue;
                    if (CliArgs.Verbose) DebugUtilities.PrintPeer($"ENEMYTURN {card.CardName} on {key} p={chance:0.###} {gain:0.##}");
                    rate += chance;
                    sum += chance * gain;
                }
            }
            if (!once) return horizon * sum;
            return rate <= 0 ? 0 : sum / rate * (1 - Math.Exp(-rate * horizon));
        }
        finally
        {
            StepChoice.Release(built);
        }
    }

    /// <summary>The live board moved on to <paramref name="enemy"/>'s next turn (or our own), shared by every card this turn.</summary>
    private static BoardState WithoutTurn(Faction enemy)
    {
        if (!WithoutTurns.TryGetValue(enemy, out BoardState turn))
            WithoutTurns[enemy] = turn = TurnLookahead.TurnOf(BoardState.Live.Fork(), enemy, WithoutBuilt);
        return turn;
    }

    /// <summary>
    /// Whether the card's own triggers accept <paramref name="probe"/>, as a block before it or a reaction
    /// after it, checked on the board before it happens: the cheap test before anything is played out.
    /// A trigger that cannot be read there counts as a yes.
    /// </summary>
    private static bool MightAnswer(CardState card, ChangeEvent probe, BoardState board)
    {
        if (!card.CardLogic.HasEventBasedTrigger) return false;
        GameSituation at = GameSituation.NewTurnOn(board);
        try { return card.CardLogic.CanBeActivated(at.OfferingForBlock(probe)) || card.CardLogic.CanBeActivated(at.ReactingTo(probe)); }
        catch (Exception) { return true; }
    }

    /// <summary>
    /// What <paramref name="enemy"/>'s turn could do to the faction's team, each with its chance this turn:
    /// the share of its hand and deck that is a battle, build or EW card, spread over the places it could go.
    /// A battle or build comes with the event to test the card's triggers on; an EW card is always played out,
    /// as a standing effect (Jet Fighters) can change what it does.
    /// </summary>
    private static IEnumerable<(string Key, double Chance, ChangeEvent Probe, Func<BoardState, List<StepOption>, BoardState> Play)> Threats(
        BoardState board, Faction enemy, Faction faction)
    {
        FactionRecord bag = board.ForFaction(enemy);
        List<CardState> cards = CardState.ForIds(bag.Hand.Concat(bag.Deck).ToList()).Where(c => c.CardData != null).ToList();
        if (cards.Count == 0) yield break;
        double Share(CardType type) => cards.Count(c => c.CardData.CardType == type) / (double)cards.Count;

        int land = Math.Max(board.AttackableLandIds(enemy).Count, 1), sea = Math.Max(board.AttackableSeaIds(enemy).Count, 1);
        foreach (Faction member in StaticGameData.FactionsForTeam(StaticGameData.FactionTeamForFaction(faction)))
        {
            if (FactionState.ForEnum(member) == null) continue;
            foreach (UnitState unit in board.ActiveUnits(member))
            {
                CountryState country = CountryState.ForId(board.Of(unit).CountryId);
                if (country == null || !board.CanAttack(enemy, country)) continue;
                bool navy = unit.Type == UnitType.NAVY;
                int unitId = unit.Id;
                yield return ($"battle {enemy} {unitId}", Share(navy ? CardType.SEA_BATTLE : CardType.LAND_BATTLE) / (navy ? sea : land),
                    new BattleUnitChangeEvent(enemy, unitId, board),
                    (b, built) => Event(new BattleUnitChangeEvent(enemy, unitId, b), b, faction, built));
            }
        }

        foreach ((List<int> spaces, CardType type) in new[] { (board.BuildableLandIds(enemy), CardType.BUILD_ARMY), (board.BuildableSeaIds(enemy), CardType.BUILD_NAVY) })
            foreach (int countryId in spaces)
                yield return ($"build {enemy} {countryId}", Share(type) / spaces.Count,
                    new DeployUnitChangeEvent(enemy, countryId, DeployType.BUILD),
                    (b, built) => Event(new DeployUnitChangeEvent(enemy, countryId, DeployType.BUILD), b, faction, built));

        foreach (IGrouping<string, CardState> copies in cards.Where(c => c.CardData.CardType == CardType.ECONOMIC_WARFARE).GroupBy(c => c.CardName))
        {
            int cardId = copies.First().Id;
            yield return ($"ew {enemy} {copies.Key}", copies.Count() / (double)cards.Count, null,
                (b, built) => EnemyTurnValue.MeasuringAs(faction, () =>
                    ProjectionValuer.ProjectCard(cardId, GameSituation.NewTurnOn(b.Fork()), enemy, true, built)?.Board));
        }
    }

    /// <summary>One enemy event on a fork of <paramref name="board"/>, with the blocks and reactions it opens; null when it cannot happen.</summary>
    private static BoardState Event(ChangeEvent changeEvent, BoardState board, Faction faction, List<StepOption> built)
    {
        built.Add(new StepOption(null, changeEvent));
        GameSituation at = GameSituation.NewTurnOn(board.Fork());
        return EnemyTurnValue.MeasuringAs(faction, () => ReactionForecast.Apply(changeEvent, at, faction, 0, built));
    }

    /// <summary>
    /// A card used at the start of the faction's turn (Conscription, Volksturm, Mobile Force): one use's gain
    /// times the turns it can be afforded. One that replaces the hand play keeps that hand card, which
    /// is worth about the play it replaces, so neither is counted.
    /// </summary>
    private static double Activation(CardState card, BoardState a, Faction faction, int horizon, bool once, List<StepOption> built)
    {
        GameSituation turn = GameSituation.NewTurnOn(a);
        if (!card.CardLogic.CanBeActivated(turn)) return 0;
        GameSituation used = ProjectionValuer.ProjectCard(card.Id, GameSituation.NewTurnOn(a.Fork()), faction, false, built);
        if (used == null) return 0;

        double gain = Firing(used.Board, a, faction, horizon);
        if (CliArgs.Verbose) DebugUtilities.PrintPeer($"OWNTURNS {card.CardName} activation {gain:0.##}");
        if (gain <= 0) return 0;
        if (once) return gain;

        FactionRecord before = a.ForFaction(faction), after = used.Board.ForFaction(faction);
        int spent = before.Hand.Count + before.Deck.Count - after.Hand.Count - after.Deck.Count;
        int uses = spent <= 0 ? horizon : Math.Min(horizon, Math.Max(KeepCardsToEndRule.Spare(faction), 0) / spent);
        return gain * uses;
    }

    /// <summary>
    /// The four cards whose worth is in the cards a faction draws, or null for any other card. Each is
    /// priced against an average draw (the mean worth of the faction's deck cards), and a better hand card
    /// counts half, as only one card a turn is played.
    /// </summary>
    private static double? Quality(CardState card, Faction faction, int horizon)
    {
        const double Realised = 0.5;
        string name = card.CardData.UniqueName;
        if (name is not ("StatusRosietheRiveter" or "StatusWomenConscripts" or "StatusSuperiorPlanning" or "ResponseRationing"))
            return null;

        double[] deck = DeckWorths(faction);
        if (deck.Length == 0) return 0;
        double draw = deck.Average();
        FactionRecord piles = BoardState.Live.ForFaction(faction);
        List<double> hand = piles.Hand.Where(id => id != card.Id).Select(id => Worth(id, faction)).OrderBy(v => v).ToList();

        switch (name)
        {
            // Two hand cards worse than a draw go under the deck now, and each turn's new draw can follow.
            case "StatusRosietheRiveter":
                return Realised * (hand.Take(2).Sum(v => Math.Max(draw - v, 0)) + horizon * deck.Average(v => Math.Max(draw - v, 0)));

            // The next draw is picked from the top four rather than taken blind; half that, as the window overlaps.
            case "StatusSuperiorPlanning":
                return horizon * Realised * 0.5 * Math.Max(BestOf(deck, 4) - draw, 0);

            // A played Build Army is drawn again instead of a deck card, and the deck keeps one card more.
            case "StatusWomenConscripts":
            {
                List<int> builds = piles.Hand.Concat(piles.Deck).Where(id => CardState.ForId(id)?.CardData.CardType == CardType.BUILD_ARMY).ToList();
                if (builds.Count == 0) return 0;
                double perUse = Math.Max(Realised * (Worth(builds[0], faction) - draw) + ProjectionValuer.CardWorth(faction), 0);
                double share = Math.Min(1.0, StaticGameData.HandSize * builds.Count / (double)(piles.Hand.Count + piles.Deck.Count));
                return horizon * share * perUse;
            }

            // The best card played goes back into the deck, and is drawn again if the game lasts that long.
            default:
                return hand.Count == 0 ? 0 : Math.Max(hand.Max(), 0) * Math.Min(1.0, horizon / (double)(piles.Deck.Count + 1));
        }
    }

    /// <summary>The worth of each card in the faction's deck as a hand card (never below 0: a dead card is just not played).</summary>
    private static double[] DeckWorths(Faction faction)
    {
        NewTurn();
        if (DeckValues.TryGetValue(faction, out double[] known)) return known;
        Dictionary<string, double> byName = new();
        List<double> values = new();
        foreach (CardState card in CardState.ForIds(BoardState.Live.ForFaction(faction).Deck.ToList()))
        {
            if (!byName.TryGetValue(card.CardName, out double v)) byName[card.CardName] = v = Worth(card.Id, faction);
            values.Add(v);
        }
        return DeckValues[faction] = values.ToArray();
    }

    /// <summary>A card's worth if played now, at least 0; a table card or one that cannot be valued counts as an average card.</summary>
    private static double Worth(int cardId, Faction faction)
    {
        CardLogic logic = CardState.ForId(cardId)?.CardLogic;
        if (logic == null || logic.IsStatus || logic.IsResponse) return ProjectionValuer.CardWorth(faction);
        return Math.Max(ProjectionValuer.ValueCard(cardId, faction) ?? ProjectionValuer.CardWorth(faction), 0);
    }

    /// <summary>The expected best of <paramref name="k"/> cards drawn from <paramref name="values"/> without replacement.</summary>
    private static double BestOf(double[] values, int k)
    {
        double[] sorted = values.OrderBy(v => v).ToArray();
        int n = sorted.Length;
        if (n <= k) return sorted.Max();
        double total = 0, all = Choose(n, k);
        for (int i = k - 1; i < n; i++) total += sorted[i] * Choose(i, k - 1) / all;
        return total;
    }

    private static double Choose(int n, int k)
    {
        double result = 1;
        for (int i = 1; i <= k; i++) result = result * (n - k + i) / i;
        return result;
    }

    /// <summary>What <paramref name="a"/> is worth over <paramref name="b"/>, its income counted over half the horizon.</summary>
    private static double Firing(BoardState a, BoardState b, Faction faction, int horizon)
    {
        int half = Math.Max(horizon / 2, 1);
        return ProjectionValuer.Value(a, faction, half) - ProjectionValuer.Value(b, faction, half);
    }

    /// <summary>The faction's play of <paramref name="cardId"/> at the start of its turn on <paramref name="board"/>.</summary>
    private static GameSituation Project(int cardId, BoardState board, Faction faction, List<StepOption> built)
        => ProjectionValuer.ProjectCard(cardId, GameSituation.NewTurnOn(board.Fork()), faction, true, built);
}
