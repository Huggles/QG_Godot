/// <summary>
/// Implemented by status cards that let one country trace supply through another's pieces, and to a
/// Supply space holding the other's Army (Total War: SHAEF). Read by BoardState.SupplyLinkFactions, which
/// the supply calculation builds its links and sources from.
/// </summary>
public interface ISupplyShareModifier : IModifier
{
    /// <summary>Whether <paramref name="tracer"/> may trace supply through <paramref name="owner"/>'s pieces while this card is in play.</summary>
    bool SharesSupply(Faction tracer, Faction owner);
}
