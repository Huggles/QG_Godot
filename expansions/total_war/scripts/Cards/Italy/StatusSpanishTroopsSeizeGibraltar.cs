using System.Collections.Generic;

/// <summary>
/// "The North Sea and Mediterranean are never adjacent for the Allies, and always adjacent for the Axis."
/// Passive: that is the North Africa strait (North Sea–Mediterranean Sea) held by the Axis whatever stands
/// in North Africa. Through BoardState.StraitController it reaches adjacency, supply, battles, building and
/// the strait icon.
/// </summary>
public partial class StatusSpanishTroopsSeizeGibraltar : StatusCardLogic, IStraitControlModifier
{
    public FactionTeam? StraitControl(StraightState strait) =>
        strait.ControllingCountryState.Country == Country.NorthAfrica ? FactionTeam.AXIS : null;

    public override TargetSet Targets() =>
        TargetSet.Countries(new List<Country> { Country.NorthSea, Country.MediterraneanSea });
}
