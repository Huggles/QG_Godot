/// <summary>
/// Implemented by status cards (or other sources) that can make a country act as a
/// supply source for a faction, dynamically extending the static IsSupply data flag.
/// Checked in GameAPI.GetSupplyCountryIds alongside the normal IsSupply check.
/// </summary>
public interface ICountryIsSupplyModifier : IModifier
{
    /// <summary>
    /// Returns true if this modifier treats the given country as a supply source for
    /// the given faction. Return false to express no opinion.
    /// </summary>
    bool GrantsIsSupply(CountryState country, Faction faction);
}
