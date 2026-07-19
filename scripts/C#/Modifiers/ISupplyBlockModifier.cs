/// <summary>
/// Implemented by status cards that block specific countries from acting as supply sources
/// for specific factions (e.g. StatusScorchedEarth: Ukraine is not a supply space for the Axis).
/// Checked in GameAPI.GetSupplyCountryIds.
/// </summary>
public interface ISupplyBlockModifier : IModifier
{
    /// <summary>
    /// Returns true if this modifier prevents the given country from being a supply source
    /// for the given faction. Return false to express no opinion.
    /// </summary>
    bool BlocksSupply(int countryId, Faction faction);
}
