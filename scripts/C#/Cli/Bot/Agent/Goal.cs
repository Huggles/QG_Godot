/// <summary>
/// What the bot is trying to achieve, and what achieving it is worth.
///
/// A goal is deliberately NOT an action. It names a place on the board and a reason to care about it;
/// turning that into "play this card, pick that country" is a separate job, done later by rules that read
/// the agenda. Keeping the two apart is what lets the same goal be served by whichever card the hand
/// happens to offer.
///
/// Every kind here is derived from the board and valued through <see cref="VpMath"/>. None of them needs
/// to know what a card does — which is why this vocabulary can be settled while the card system is still
/// being reworked.
/// </summary>
public enum GoalKind
{
    /// <summary>An unoccupied supply star this faction can deploy into. Worth +2 VP/turn once held.</summary>
    TakeEmptyStar,

    /// <summary>
    /// An enemy-held supply star this faction can attack. A country locks to its first occupier's team
    /// (CountryState.OccupyingTeam), so an enemy star cannot be shared into — it has to be emptied first
    /// and taken afterwards. That is two goals over two turns, not one, and they are kept separate so the
    /// agenda says which half it is asking for.
    /// </summary>
    ClearEnemyStar,

    /// <summary>A supply star this faction holds that an enemy can attack. Worth the income it protects.</summary>
    HoldThreatenedStar,

    /// <summary>
    /// A non-scoring country worth occupying because it puts stars within reach that are not reachable
    /// now. Pays nothing itself, so it is valued at a discount on what it opens up.
    /// </summary>
    ExtendSupplyReach,

    /// <summary>
    /// A unit standing on a star with its supply line cut. The SUPPLY step removes out-of-supply units,
    /// so this is income already on the clock unless the chain is restored.
    /// </summary>
    KeepUnitsInSupply,
}

/// <summary>
/// One goal: a place, a reason, and a price.
///
/// <paramref name="Value"/> is denominated in **victory points over the rest of the game** — the
/// per-turn VP effect multiplied by the rounds remaining, then scaled by the faction profile's weight for
/// this kind. Holding every goal to one unit is what makes them comparable at all: a faction weight
/// becomes an honest "this is worth 1.4x its VP to me" rather than a number picked to outrank another
/// number. It also means a goal's value falls as the game runs out, without anybody coding a rule for
/// that.
///
/// <paramref name="Why"/> is for the trace only. Nothing branches on it.
/// </summary>
public readonly record struct Goal(GoalKind Kind, int TargetCountryId, double Value, string Why);
