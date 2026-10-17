using System.Collections.Generic;

/// <summary>
/// "If Western Europe is occupied by an Axis Army, the United Kingdom is France's Home space." Passive:
/// through BoardState.HomeSpaceOf, read from the board each time, so the Home space returns to Western
/// Europe as soon as no Axis Army stands there. France builds in the United Kingdom without a supplied
/// neighbour meanwhile, and its flag circle moves there.
/// </summary>
public partial class StatusGovernmentinExile : StatusCardLogic, IHomeSpaceModifier
{
    public CountryState HomeSpace(BoardState board, Faction faction) =>
        faction == Faction.FRANCE && TotalWarCards.TeamUnitsIn(board, FactionTeam.AXIS, UnitType.ARMY, new[] { Country.WesternEurope }).Count > 0
            ? CountryState.ForEnum(Country.UnitedKingdom)
            : null;

    public override TargetSet Targets() => TargetSet.Countries(new List<Country> { Country.WesternEurope, Country.UnitedKingdom });
}
