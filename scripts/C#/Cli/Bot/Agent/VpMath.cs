using System;
using System.Collections.Generic;

/// <summary>
/// The game's own victory-point formula, exposed so the bot can value a board position and the CHANGE a
/// candidate move would make to it.
///
/// **This is a mirror of <see cref="VictoryStepHandlerDefault.ScoreSupplyCountryVPs"/> and must not drift
/// from it.** That method is the authority; if its formula changes, this file is wrong until it is
/// changed too. Nothing here mutates state or draws from any RNG stream.
///
/// The formula it mirrors is <c>Math.Max(3 - cs.Units.Keys.Count, 1)</c>, paid once per turn for every
/// supply country a faction occupies. Two properties of it drive the whole design:
///
///  - **It is exact and cheap.** The bot does not need to estimate positional value; it can compute the
///    actual number the game will pay. No heuristic, no tuning constant, no search.
///  - **It is LOCAL.** <see cref="CountryState.Units"/> is keyed by faction, so the payout for a country
///    depends only on who is standing in that one country. A deploy or a battle changes exactly one
///    country's occupancy, so the team-VP delta of a candidate move is an O(occupants ≤ 3) recompute of
///    that single country — not a sweep of the board.
///
/// **The occupant count is never zero, and that is easy to get wrong.** The scoring loop walks
/// <see cref="FactionState.OccupiedCountryIds"/>, which is built from the faction's own active units, so
/// every country it scores already counts that faction as an occupant. The real payouts are therefore:
///
/// <code>
///   occupants   VP each   team total
///       1          2           2
///       2          1           2
///       3          1           3
/// </code>
///
/// A solo star pays **2**, not 3. And joining a teammate's star is **team-neutral** — the newcomer gains
/// 1 while the teammate drops from 2 to 1 — so it is a wasted unit, not a small gain. Both of those read
/// the other way round in <see cref="PreferSupplyStarDeployRule"/>'s doc-comment, which derives its
/// weights from a 3/2 payout that cannot occur. Computing the number instead of deriving it by hand is
/// the point of this class.
/// </summary>
public static class VpMath
{
    /// <summary>
    /// What <paramref name="country"/> pays <paramref name="faction"/> per turn, right now.
    /// Zero unless it is a supply country the faction actually occupies.
    /// </summary>
    public static int StarVp(CountryState country, Faction faction)
    {
        if (country == null || !country.IsSupply) return 0;
        if (!country.Units.ContainsKey(faction)) return 0;
        return PayoutFor(country.Units.Count);
    }

    /// <summary>
    /// What <paramref name="country"/> pays <paramref name="team"/> per turn, summed over however many of
    /// its factions are standing there.
    ///
    /// A country locks to its first occupier's team (<see cref="CountryState.OccupyingTeam"/>), so in
    /// practice this is either the whole payout or nothing. It is still written as a sum so the caller
    /// does not have to know that, and so a rules change that permitted mixed occupancy would not quietly
    /// make this wrong.
    /// </summary>
    public static int TeamStarVpAt(CountryState country, FactionTeam team)
        => country == null ? 0 : TeamStarVpAt(country, country.Units, team);

    /// <summary>As above, for a country whose occupants come from somewhere other than the live board (a fork).</summary>
    public static int TeamStarVpAt(CountryState country, IReadOnlyDictionary<Faction, int> units, FactionTeam team)
    {
        if (country == null || !country.IsSupply || units == null) return 0;
        return OccupantsOnTeam(units, team) * PayoutFor(units.Count);
    }

    /// <summary>
    /// The change to <paramref name="faction"/>'s TEAM per-turn VP if it gained a unit in
    /// <paramref name="country"/>.
    ///
    /// Zero when the faction is already there: a deploy onto a country you occupy is a real, reactable
    /// event (see <see cref="CountryState.CanBuild"/>) but the board — and so the payout — is unchanged.
    ///
    /// Assumes the deploy is legal; legality is the offer set's job, never this class's.
    /// </summary>
    public static int VpDeltaOfDeploy(CountryState country, Faction faction)
    {
        if (country == null || !country.IsSupply) return 0;
        if (country.Units.ContainsKey(faction)) return 0;

        FactionTeam team = StaticGameData.FactionTeamForFaction(faction);
        int before = TeamStarVpAt(country, team);
        int after = (OccupantsOnTeam(country, team) + 1) * PayoutFor(country.Units.Count + 1);
        return after - before;
    }

    /// <summary>
    /// The change to <paramref name="faction"/>'s TEAM per-turn VP if its unit left
    /// <paramref name="country"/> — whether removed by a battle, by supply, or by a card.
    ///
    /// The sign is always "what this does to that faction's own team", so removing your own unit returns
    /// a negative number and a caller weighing an attack on an enemy star wants the negation of this.
    /// Stating it once here is cheaper than every caller re-deciding which way is up.
    /// </summary>
    public static int VpDeltaOfRemoval(CountryState country, Faction faction)
    {
        if (country == null || !country.IsSupply) return 0;
        if (!country.Units.ContainsKey(faction)) return 0;

        FactionTeam team = StaticGameData.FactionTeamForFaction(faction);
        int before = TeamStarVpAt(country, team);

        int occupantsAfter = country.Units.Count - 1;
        int teamOccupantsAfter = OccupantsOnTeam(country, team) - 1;
        int after = teamOccupantsAfter <= 0 || occupantsAfter <= 0
            ? 0
            : teamOccupantsAfter * PayoutFor(occupantsAfter);

        return after - before;
    }

    /// <summary>
    /// Everything <paramref name="team"/> currently earns per turn: supply stars plus status cards.
    ///
    /// A board sweep, unlike the delta helpers — for the periodic assessment, not for scoring a candidate
    /// option.
    /// </summary>
    public static int TeamVpRate(FactionTeam team)
        => SupplyStarVpRate(team) + StatusCardVpRate(team);

    /// <summary>
    /// The board half of <see cref="TeamVpRate"/>: supply stars only, no cards.
    ///
    /// Walks countries rather than each faction's OccupiedCountryIds so a shared star is visited once and
    /// its payout is computed once.
    /// </summary>
    public static int SupplyStarVpRate(FactionTeam team) => SupplyStarVpRate(team, BoardState.Live);

    /// <summary>The board half of the rate on <paramref name="board"/>.</summary>
    public static int SupplyStarVpRate(FactionTeam team, BoardState board)
    {
        List<CountryState> countries = CountryState.AllCountryStates;
        if (countries == null) return 0;

        int total = 0;
        for (int i = 0; i < countries.Count; i++)
        {
            CountryState country = countries[i];
            if (country == null) continue;
            total += TeamStarVpAt(country, board.UnitsIn(country), team);
        }
        return total;
    }

    /// <summary>
    /// The status-card half of <see cref="TeamVpRate"/>.
    ///
    /// **The one place in the bot's agent layer that reads anything card-derived**, deliberately isolated
    /// to a single method: the card system is being reworked, and this is the only seam that rework has to
    /// touch here. It cannot simply be dropped — twelve status cards implement
    /// <see cref="IVPModifier"/> and a VP rate that ignored them would be wrong by a wide margin on a
    /// developed board.
    ///
    /// Each modifier is guarded individually. <see cref="IVPModifier.AddVictoryPoints"/> is a pure query
    /// in every current implementation, but it is card-authored code reading live state (indexing a
    /// scoring-country list, dereferencing FactionData), so one card throwing must cost its own
    /// contribution and nothing else. Silently returning a slightly low rate is a bad answer; aborting
    /// the prompt over a display-detail exception is a worse one.
    /// </summary>
    public static int StatusCardVpRate(FactionTeam team)
    {
        int total = 0;
        foreach (IVPModifier modifier in ModifierRegistry.GetAll<IVPModifier>())
        {
            if (StaticGameData.FactionTeamForFaction(modifier.Faction) != team) continue;

            try { total += modifier.AddVictoryPoints()?.VictoryPoints ?? 0; }
            catch { /* see summary: one card's failure costs one card's points */ }
        }
        return total;
    }

    /// <summary>
    /// The payout formula itself, for a country held by <paramref name="occupantCount"/> distinct
    /// factions. Kept in one place so every helper above shares it verbatim.
    /// </summary>
    private static int PayoutFor(int occupantCount) => Math.Max(3 - occupantCount, 1);

    /// <summary>How many of <paramref name="country"/>'s occupying factions belong to <paramref name="team"/>.</summary>
    private static int OccupantsOnTeam(CountryState country, FactionTeam team) => OccupantsOnTeam(country.Units, team);

    private static int OccupantsOnTeam(IReadOnlyDictionary<Faction, int> units, FactionTeam team)
    {
        int count = 0;
        foreach (Faction occupant in units.Keys)
            if (StaticGameData.FactionTeamForFaction(occupant) == team) count++;
        return count;
    }
}
