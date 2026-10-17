using System.Collections.Generic;

/// <summary>
/// "Siberia is now the Soviet Union Home space, and a Supply space for the Soviet Union (only). Place a
/// Home space marker on Siberia. Moscow is no longer a Supply space for any country." Passive, through
/// three modifiers: the Home space (building without a supplied neighbour, the flag circle), Siberia's
/// star for the Soviet Union, and Moscow's star for no one (BoardState.IsSupplySpace: supply and VP).
/// </summary>
public partial class StatusGovernmentEvacuatesToKuibyshev : StatusCardLogic, IHomeSpaceModifier, ISupplySpaceModifier, ISupplyBlockModifier
{
    public CountryState HomeSpace(BoardState board, Faction faction) =>
        faction == Faction.SOVIET ? CountryState.ForEnum(Country.Siberia) : null;

    public bool AddsSupplySpace(int countryId, Faction faction) =>
        countryId == (int)Country.Siberia && faction == Faction.SOVIET;

    public bool BlocksSupply(int countryId, Faction faction) => countryId == (int)Country.Moscow;

    public override TargetSet Targets() => TargetSet.Countries(new List<Country> { Country.Siberia, Country.Moscow });
}
