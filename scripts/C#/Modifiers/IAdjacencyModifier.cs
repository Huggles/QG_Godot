using System.Collections.Generic;

/// <summary>
/// Implemented by status cards that make spaces adjacent for some countries only (Total War: Turkey
/// Opens the Straits). Read by BoardState.AdjacentCountryIds, which supply, building, battles and the
/// faction-aware card checks all go through.
/// </summary>
public interface IAdjacencyModifier : IModifier
{
    /// <summary>The spaces <paramref name="faction"/> may also treat as next to <paramref name="country"/> while this card is in play.</summary>
    IEnumerable<int> ExtraAdjacentCountryIds(Faction faction, CountryState country);
}
