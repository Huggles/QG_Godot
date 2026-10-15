using System.Collections.Generic;
using System.Linq;

public partial class EventSiberianTransfer : EventCardLogic
{
    /// <summary>Siberia and its neighbours. Read when needed, not at class load, which runs before the map is.</summary>
    private static List<Country> SiberiaAndNeighbours =>
        CountryState.ForEnum(Country.Siberia).ConnectedCountryStates.Select(country => country.Country).Append(Country.Siberia).ToList();

    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.RECRUIT, UnitType.ARMY, SiberiaAndNeighbours));

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeployStep(this, DeployType.RECRUIT, UnitType.ARMY, SiberiaAndNeighbours, "Recruit an Army in or adjacent to Siberia")
    };
}
