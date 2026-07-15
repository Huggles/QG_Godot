/// <summary>
/// Implemented by status cards (or other sources) that can make a country act as a
/// supply link node for a faction during pathfinding, even if the faction does not
/// occupy it. Checked in PathFindingNodeDefault.CountryLinksSupplyForFaction.
/// </summary>
public interface ICountryProvidesSupplyModifier : IModifier
{
    /// <summary>
    /// Returns true if this modifier allows the given country to be traversed in the
    /// supply pathfinding graph for the given faction. Return false to express no opinion.
    /// </summary>
    bool ProvidesSupplyLink(CountryState country, Faction faction);
}
