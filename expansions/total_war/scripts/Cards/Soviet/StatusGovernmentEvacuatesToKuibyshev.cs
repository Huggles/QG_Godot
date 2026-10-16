using System.Collections.Generic;

/// <summary>
/// "Siberia is now the Soviet Union Home space, and a Supply space for the Soviet Union (only). Place a
/// Home space marker on Siberia. Moscow is no longer a Supply space for any country." So far only the
/// Home space moves (IHomeSpaceModifier), which puts the Soviet flag circle on Siberia. The supply half,
/// and the rules that read the Home space (building there), are still to come.
/// </summary>
public partial class StatusGovernmentEvacuatesToKuibyshev : StatusCardLogic, IHomeSpaceModifier
{
    public CountryState HomeSpace(BoardState board, Faction faction) =>
        faction == Faction.SOVIET ? CountryState.ForEnum(Country.Siberia) : null;

    public override TargetSet Targets() => TargetSet.Countries(new List<Country> { Country.Siberia });
}
