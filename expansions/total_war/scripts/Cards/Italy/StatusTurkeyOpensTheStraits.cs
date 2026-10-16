using System.Collections.Generic;
using System.Linq;

/// <summary>
/// "For the Axis, the Middle East is adjacent to the Balkans, and the Black Sea is adjacent to the
/// Mediterranean Sea." Passive: through BoardState.AdjacentCountryIds the two links reach supply,
/// building, battles and every Axis card's adjacency check. The Allies never see them.
/// </summary>
public partial class StatusTurkeyOpensTheStraits : StatusCardLogic, IAdjacencyModifier
{
    private static readonly (Country, Country)[] Links =
    {
        (Country.MiddleEast, Country.Balkans),
        (Country.BlackSea, Country.MediterraneanSea),
    };

    public IEnumerable<int> ExtraAdjacentCountryIds(Faction faction, CountryState country)
    {
        if (StaticGameData.FactionTeamForFaction(faction) != FactionTeam.AXIS) yield break;
        foreach ((Country a, Country b) in Links)
        {
            if (country.Country == a) yield return (int)b;
            else if (country.Country == b) yield return (int)a;
        }
    }

    public override TargetSet Targets() =>
        TargetSet.Countries(Links.SelectMany(link => new[] { link.Item1, link.Item2 }).ToList());
}
