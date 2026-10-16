/// <summary>
/// Implemented by status cards that move a country's Home space (Total War: Government Evacuates to
/// Kuibyshev, Government in Exile). Read by BoardState.HomeSpaceOf, which the map's Home space marker
/// follows.
/// </summary>
public interface IHomeSpaceModifier : IModifier
{
    /// <summary>The space that is <paramref name="faction"/>'s Home space while this card is in play, or null for no say.</summary>
    CountryState HomeSpace(BoardState board, Faction faction);
}
