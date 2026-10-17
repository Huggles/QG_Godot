/// <summary>
/// Implemented by status cards that stop specific countries being Supply spaces for specific factions
/// (e.g. StatusScorchedEarth: Ukraine is not a Supply space for the Axis): no supply source and no star
/// VP. Checked in BoardState.IsSupplySpace.
/// </summary>
public interface ISupplyBlockModifier : IModifier
{
    /// <summary>
    /// Returns true if this modifier prevents the given country from being a supply source
    /// for the given faction. Return false to express no opinion.
    /// </summary>
    bool BlocksSupply(int countryId, Faction faction);
}
