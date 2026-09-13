using System.Collections.Generic;

/// <summary>
/// One faction's read of the board, taken fresh at a decision point.
///
/// Pure: it allocates lists and reads live state, and does nothing else. No mutation, no
/// <see cref="GameStateCalculator"/> call, and — the invariant that matters most — **no RNG draw of any
/// kind**. The bot's reproducibility rests on a single counted draw stream owned by
/// <see cref="RandomInputProvider"/>; a perception layer that consumed a draw would silently decouple
/// <c>decision_seed</c> from the decisions it is supposed to reproduce.
///
/// Reads live host state through the usual statics, which is sound for the same reason
/// <see cref="BotDecision"/> gives: the bot runs on the host, where everything
/// <see cref="GameStateCalculator"/> computes is current. That calculator also computes tags for EVERY
/// playable faction, not just the one being asked, which is what makes enemy-perspective questions —
/// "can any of them attack this star of mine?" — answerable without re-deriving adjacency here.
///
/// Cost is O(countries + units): roughly 80 countries and 30 units, against a
/// <c>GameStateCalculator.CalculateAll</c> that already sweeps six factions over the same board about
/// 1100 times a game. Built per prompt; if that ever registers in <c>sim_perf</c>, memoize on
/// (GameTurn, TurnStep, Faction) rather than building it less often.
/// </summary>
public sealed class BoardAssessment
{
    /// <summary>The faction this is a read FOR. Every "my"/"enemy" field below is relative to it.</summary>
    public Faction Faction { get; private set; }
    public FactionTeam Team { get; private set; }
    public FactionTeam EnemyTeam { get; private set; }

    public int Round { get; private set; }

    /// <summary>
    /// Rounds still to play, floored at zero. The horizon a per-turn VP rate is worth multiplying by: a
    /// star taken in round 18 of 20 is worth a fraction of the same star taken in round 3.
    /// </summary>
    public int RoundsRemaining { get; private set; }

    /// <summary>
    /// What one point of per-turn income is worth from here on. Floored at 1 so that a goal in the last
    /// round still has a value at all — an agenda that went uniformly zero would rank by nothing, and
    /// "the game is nearly over" is a reason to prefer the biggest swing, not to stop choosing.
    /// </summary>
    public int Horizon => RoundsRemaining < 1 ? 1 : RoundsRemaining;

    public int MyScore { get; private set; }
    public int EnemyScore { get; private set; }

    /// <summary>Positive when my team is ahead. The game ends at a 30-point lead either way.</summary>
    public int ScoreGap => MyScore - EnemyScore;

    /// <summary>Per-turn income, supply stars plus status cards. See <see cref="VpMath.TeamVpRate"/>.</summary>
    public int MyVpRate { get; private set; }
    public int EnemyVpRate { get; private set; }

    public int ArmiesAvailable { get; private set; }
    public int NaviesAvailable { get; private set; }

    public int UnitsInSupply { get; private set; }

    /// <summary>
    /// Units that will be removed at the next SUPPLY step unless the chain is restored — see
    /// SupplyStepHandlerDefault. A standing loss already on the clock, not a hypothetical.
    /// </summary>
    public int UnitsOutOfSupply { get; private set; }

    /// <summary>Empty supply stars this faction could deploy into right now (Tag.Buildable).</summary>
    public IReadOnlyList<int> TakeableStarIds { get; private set; }

    /// <summary>
    /// Supply stars this faction occupies whose garrison SOME enemy faction can attack right now. Read
    /// from the enemy's own Tag.Attackable on the unit standing there rather than re-derived, so it
    /// agrees with what that enemy will actually be offered — see <see cref="IsAttackableBy"/>.
    /// </summary>
    public IReadOnlyList<int> ThreatenedStarIds { get; private set; }

    /// <summary>Enemy-held supply stars holding a unit this faction can attack.</summary>
    public IReadOnlyList<int> AttackableEnemyStarIds { get; private set; }

    /// <summary>Supply stars this faction occupies, threatened or not.</summary>
    public IReadOnlyList<int> HeldStarIds { get; private set; }

    /// <summary>
    /// Countries this faction can deploy into that pay nothing themselves — every buildable non-star.
    /// The raw material for staging: worth something only in proportion to what standing there puts in
    /// reach next turn, which is <see cref="ExtendSupplyReachProposer"/>'s job to work out.
    /// </summary>
    public IReadOnlyList<int> BuildableNonStarIds { get; private set; }

    private BoardAssessment() { }

    /// <summary>
    /// Take the read. Returns an empty-but-valid assessment rather than throwing if there is no game
    /// state yet — the caller is answering a prompt and must not be derailed by perception.
    /// </summary>
    public static BoardAssessment For(Faction faction)
    {
        BoardAssessment a = new()
        {
            Faction = faction,
            Team = StaticGameData.FactionTeamForFaction(faction),
            EnemyTeam = StaticGameData.OpponentFactionTeamForFaction(faction),
            TakeableStarIds = new List<int>(),
            ThreatenedStarIds = new List<int>(),
            AttackableEnemyStarIds = new List<int>(),
            HeldStarIds = new List<int>(),
            BuildableNonStarIds = new List<int>(),
        };

        GameFlow flow = GameFlow.Instance;
        a.Round = flow?.Round ?? 0;
        a.RoundsRemaining = flow == null ? 0 : System.Math.Max(flow.MaxRound - flow.Round, 0);

        a.MyScore = StaticGameData.ScoreForTeam(a.Team);
        a.EnemyScore = StaticGameData.ScoreForTeam(a.EnemyTeam);
        a.MyVpRate = VpMath.TeamVpRate(a.Team);
        a.EnemyVpRate = VpMath.TeamVpRate(a.EnemyTeam);

        a.ArmiesAvailable = UnitPool.AvailableUnitCount(faction, UnitType.ARMY);
        a.NaviesAvailable = UnitPool.AvailableUnitCount(faction, UnitType.NAVY);

        FactionState state = FactionState.ForEnum(faction);
        a.UnitsInSupply = state?.SuppliedUnitIds?.Count ?? 0;
        a.UnitsOutOfSupply = state?.UnsuppliedUnitIds?.Count ?? 0;

        a.ClassifyStars();
        return a;
    }

    /// <summary>
    /// The single pass over the board that fills the four star lists.
    ///
    /// One loop rather than four queries: each country is a handful of cheap tag reads, and walking
    /// CountryState.AllCountryStates once keeps the lists in stable board order — which is what makes a
    /// goal agenda built from them reproducible across runs of the same seed.
    /// </summary>
    private void ClassifyStars()
    {
        List<CountryState> countries = CountryState.AllCountryStates;
        if (countries == null) return;

        List<Faction> enemies = StaticGameData.FactionsForTeam(EnemyTeam);
        List<int> takeable = new();
        List<int> threatened = new();
        List<int> attackableEnemy = new();
        List<int> held = new();
        List<int> buildableNonStar = new();

        foreach (CountryState country in countries)
        {
            if (country == null) continue;

            if (!country.IsSupply)
            {
                if (country.Tags.Has(Tag.Buildable, Faction)) buildableNonStar.Add(country.Id);
                continue;
            }

            if (country.Units.ContainsKey(Faction))
            {
                held.Add(country.Id);
                if (IsAttackableBy(country, Faction, enemies)) threatened.Add(country.Id);
                continue;
            }

            if (country.IsCountryEmpty)
            {
                if (country.Tags.Has(Tag.Buildable, Faction)) takeable.Add(country.Id);
                continue;
            }

            if (country.OccupyingTeam == EnemyTeam && HoldsAttackableUnit(country, Faction))
                attackableEnemy.Add(country.Id);

            // A star held only by a TEAMMATE falls through every branch on purpose, and is the one case
            // worth naming because its absence looks like an oversight. Joining it is team-VP-neutral —
            // the newcomer gains 1 and the teammate drops from 2 to 1 — so there is nothing to want, and
            // VpMath.VpDeltaOfDeploy would price it at exactly 0 if it were offered as a goal anyway.
        }

        TakeableStarIds = takeable;
        ThreatenedStarIds = threatened;
        AttackableEnemyStarIds = attackableEnemy;
        HeldStarIds = held;
        BuildableNonStarIds = buildableNonStar;
    }

    /// <summary>
    /// Whether <paramref name="owner"/>'s unit in this country can be attacked by any of
    /// <paramref name="enemies"/> right now.
    ///
    /// **Read from the UNIT, not from the country, and the difference is not cosmetic.**
    /// <see cref="Tag.Attackable"/> on a CountryState means something narrower than it sounds: AttackOption
    /// only adds a country to AttackableCountries when <c>OccupyingTeam == NONE</c>, so the country-level
    /// tag means "empty, and I may walk into it". An OCCUPIED country never carries it. Enemy pieces are
    /// marked by tagging the units themselves (AttackOption.AttackableUnits), which is what this reads.
    ///
    /// Asking the country instead compiles, runs, and silently answers false for every occupied square on
    /// the board — which is exactly what it did here, leaving two proposers reporting zero goals across a
    /// full 20-round game.
    ///
    /// Reading the tag rather than re-deriving with CountryState.CanAttack also picks up
    /// <see cref="UnitState.ImmuneForTurn"/> for free: AttackOption excludes immune units when it builds
    /// the list, so a unit that cannot be touched this turn is correctly not counted as threatened.
    /// </summary>
    private static bool IsAttackableBy(CountryState country, Faction owner, List<Faction> enemies)
    {
        if (!country.Units.TryGetValue(owner, out int unitId)) return false;

        UnitState unit = UnitById(unitId);
        if (unit == null) return false;

        foreach (Faction enemy in enemies)
            if (unit.Tags.Has(Tag.Attackable, enemy)) return true;
        return false;
    }

    /// <summary>
    /// Whether this country holds any unit <paramref name="attacker"/> can hit. See
    /// <see cref="IsAttackableBy"/> for why this reads the units and not the country.
    /// </summary>
    private static bool HoldsAttackableUnit(CountryState country, Faction attacker)
    {
        foreach (int unitId in country.Units.Values)
        {
            UnitState unit = UnitById(unitId);
            if (unit != null && unit.Tags.Has(Tag.Attackable, attacker)) return true;
        }
        return false;
    }

    /// <summary>
    /// UnitState.ForId indexes the id map directly and throws on an id a nested reaction has already
    /// removed. Perception must not be the thing that ends a run, so this asks rather than assumes.
    /// </summary>
    private static UnitState UnitById(int unitId)
    {
        Dictionary<int, UnitState> byId = GameSession.Current?.GameState?.UnitStatesById;
        if (byId == null) return null;
        return byId.TryGetValue(unitId, out UnitState unit) ? unit : null;
    }

    /// <summary>
    /// One-line form for the bot trace. Kept terse and stable — it is emitted on every prompt, and a run
    /// of the same (seed, decision_seed) must produce byte-identical lines for the trace to be diffable.
    /// </summary>
    public string Headline =>
        $"r{Round}/{Round + RoundsRemaining} score {MyScore}:{EnemyScore} rate {MyVpRate}:{EnemyVpRate} " +
        $"stars held={HeldStarIds.Count} threat={ThreatenedStarIds.Count} take={TakeableStarIds.Count} " +
        $"hit={AttackableEnemyStarIds.Count} pool={ArmiesAvailable}a/{NaviesAvailable}n " +
        $"supply={UnitsInSupply}in/{UnitsOutOfSupply}out";
}
