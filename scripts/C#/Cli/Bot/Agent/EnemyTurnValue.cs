using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// What a Status or Response card on the table is worth over the enemy turns still to come — the half
/// of its value a line of own turns (<see cref="TurnLookahead"/>) never sees. Radar is the case: it
/// does nothing on the US turn and saves a Navy on a German one. <c>bot_enemy_turns=all|none|AXIS|ALLIES</c>
/// turns it on, none by default.
///
/// Nothing here knows any card. For each enemy faction, every card still in its hand or deck — one
/// bag: which is which is hidden — is played out on two forks at the start of that faction's next
/// turn, one with the card on our table and one without, and the card's worth against that play is
/// the difference it makes to our team's value, less the difference it makes standing still. A card
/// is a trigger when that difference is not zero. The card's own triggers and steps decide all of it,
/// block windows included (see <see cref="ReactionForecast"/>):
///
/// <code>
/// value = Σ over enemy factions e:  turns left × Σ_c (copies of c / cards in e's bag) × Δ_c
/// </code>
///
/// so it falls when no enemy piece is in reach (Δ is 0), when the enemy's triggering cards run out
/// (fewer copies in the bag), and late in the game (fewer turns, and less income left to save). Income
/// a firing saves is counted over half the horizon: a firing comes, on average, halfway through it.
///
/// The enemy is assumed to play a card at random from its bag, and one turn's board stands in for all
/// of them. Measured once per card while the board stands still, then scaled to the turn the card is
/// played in; the card-free half of the measurement is shared by every card (see <see cref="NewDecision"/>).
/// </summary>
public static class EnemyTurnValue
{
    public static bool Applies(Faction faction) => BotSides.Includes("bot_enemy_turns", "none", faction);

    /// <summary>True while a measurement runs: its projections forecast block windows whoever values them.</summary>
    public static bool Measuring => MeasuringFor != Faction.NONE;

    /// <summary>
    /// The faction measuring, while a measurement runs, else NONE. It knows its own team's hidden
    /// Responses even though the enemy whose plays are projected does not: see ReactionForecast.Candidates.
    /// </summary>
    public static Faction MeasuringFor { get; private set; } = Faction.NONE;

    /// <summary>Per card: the value of one enemy turn, split into its now part and its income per round.</summary>
    private static readonly Dictionary<int, (double Now, double PerRound)?> Measured = new();

    /// <summary>
    /// The half of every measurement that does not depend on the card: per enemy, the board at the start
    /// of its turn without the card, and its play of each card in its bag there. Shared by every card
    /// measured until the board moves on, and released with it.
    /// </summary>
    private static readonly Dictionary<Faction, (BoardState Turn, Dictionary<string, BoardState> Plays)> Without = new();
    private static readonly List<StepOption> WithoutBuilt = new();
    private static BoardState _withoutStart;
    private static (int Turn, Faction Faction, int Hand, int Units) _stamp;

    /// <summary>
    /// Called before each decision: keeps the measurements while the board is the one they were taken
    /// on — the same turn, the same faction asking, the same hand and the same pieces in play, which
    /// is what a free activation or a lost unit would change — and forgets them otherwise.
    /// </summary>
    public static void NewDecision(Faction faction)
    {
        BoardState live = BoardState.Live;
        var stamp = (live.GameTurn, faction, live.ForFaction(faction).Hand.Count,
                     StaticGameData.PlayableFactions.Sum(f => live.ActiveUnitIds(f).Count));
        if (stamp == _stamp && _withoutStart != null) return;

        _stamp = stamp;
        Measured.Clear();
        Without.Clear();
        StepChoice.Release(WithoutBuilt);
        WithoutBuilt.Clear();
        _withoutStart = null;
    }

    /// <summary>The card's worth over the enemy turns in <paramref name="horizon"/> rounds.</summary>
    public static double Of(int cardId, Faction faction, int horizon)
    {
        if (!Measured.TryGetValue(cardId, out var turn)) Measured[cardId] = turn = Measure(cardId, faction);
        if (turn is not { } t) return 0;
        return horizon * (t.Now + t.PerRound * Math.Max(horizon / 2.0, 1));
    }

    private static (double Now, double PerRound)? Measure(int cardId, Faction faction)
    {
        CardState card = CardState.ForId(cardId);
        if (card?.CardLogic == null) return null;

        List<StepOption> built = new();
        MeasuringFor = faction;
        try
        {
            BoardState without = _withoutStart ??= BoardState.Live.Fork();
            BoardState with = BoardState.Live.Fork();
            PlayCardChangeEvent play = new(cardId) { IsTrigger = false };
            built.Add(new StepOption(null, play));
            if (!with.Apply(play, GameSituation.Live.WithBoard(with), cardTags: false)) return null;

            double now = 0, perRound = 0;
            foreach (Faction enemy in StaticGameData.FactionsForTeam(StaticGameData.OpponentFactionTeamForFaction(faction)))
            {
                if (FactionState.ForEnum(enemy) == null) continue;
                if (!Without.TryGetValue(enemy, out var baseline))
                    Without[enemy] = baseline = (TurnLookahead.TurnOf(without, enemy, WithoutBuilt), new Dictionary<string, BoardState>());
                BoardState a = TurnLookahead.TurnOf(with, enemy, built), b = baseline.Turn;
                if (a == null || b == null) continue;

                (double standNow, double standRate) = Split(a, b, faction);
                FactionRecord bag = with.ForFaction(enemy);
                List<CardState> cards = CardState.ForIds(bag.Hand.Concat(bag.Deck).ToList());
                if (cards.Count == 0) continue;

                double sumNow = 0, sumRate = 0;
                int triggers = 0, unvalued = 0;
                foreach (IGrouping<string, CardState> copies in cards.GroupBy(c => c.CardName))
                {
                    int id = copies.First().Id;
                    if (!baseline.Plays.TryGetValue(copies.Key, out BoardState tb))
                        baseline.Plays[copies.Key] = tb = Project(id, b, enemy, WithoutBuilt)?.Board;
                    GameSituation ta = Project(id, a, enemy, built);
                    if (ta == null || tb == null) { unvalued += copies.Count(); continue; }

                    (double dNow, double dRate) = Split(ta.Board, tb, faction);
                    dNow -= standNow;
                    dRate -= standRate;
                    if (Math.Abs(dNow) < 1e-9 && Math.Abs(dRate) < 1e-9) continue;

                    triggers += copies.Count();
                    sumNow += copies.Count() * dNow;
                    sumRate += copies.Count() * dRate;
                }

                now += sumNow / cards.Count;
                perRound += sumRate / cards.Count;
                if (CliArgs.Verbose)
                    DebugUtilities.PrintPeer($"ENEMYTURNS {card.CardName} e={enemy} q={triggers}/{cards.Count} unvalued={unvalued} " +
                                             $"now={sumNow / Math.Max(triggers, 1):0.##} perRound={sumRate / Math.Max(triggers, 1):0.##}");
            }
            return (now, perRound);
        }
        finally
        {
            MeasuringFor = Faction.NONE;
            StepChoice.Release(built);
        }
    }

    /// <summary>
    /// What <paramref name="a"/> is worth to <paramref name="faction"/>'s team over <paramref name="b"/>,
    /// as the part that does not depend on the horizon and the income per round.
    /// </summary>
    private static (double Now, double PerRound) Split(BoardState a, BoardState b, Faction faction)
    {
        double at0 = ProjectionValuer.Value(a, faction, 0) - ProjectionValuer.Value(b, faction, 0);
        double at1 = ProjectionValuer.Value(a, faction, 1) - ProjectionValuer.Value(b, faction, 1);
        return (at0, at1 - at0);
    }

    /// <summary>The enemy's best play of <paramref name="cardId"/> at the start of its turn on <paramref name="board"/>.</summary>
    private static GameSituation Project(int cardId, BoardState board, Faction enemy, List<StepOption> built)
        => ProjectionValuer.ProjectCard(cardId, GameSituation.NewTurnOn(board.Fork()), enemy, true, built);
}
