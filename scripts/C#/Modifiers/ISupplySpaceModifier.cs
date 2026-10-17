/// <summary>
/// Implemented by status cards that make a space a Supply space for specific countries although the map
/// has no star there (Total War: Government Evacuates to Kuibyshev, Siberia for the Soviet Union). The
/// counterpart of ISupplyBlockModifier; both are read by BoardState.IsSupplySpace, and a block wins.
/// </summary>
public interface ISupplySpaceModifier : IModifier
{
    /// <summary>Whether the given country is a Supply space for the given faction while this card is in play. False for no say.</summary>
    bool AddsSupplySpace(int countryId, Faction faction);
}
