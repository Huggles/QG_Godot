using System.Collections.Generic;

public partial class EventTheBrazilianExpeditionaryForce : EventCardLogic
{
    private static readonly List<Country> armySpaces = [Country.LatinAmerica];
    private static readonly List<Country> navySpaces = [Country.SouthAtlantic, Country.SouthernOcean];

    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.BUILD, UnitType.ARMY, armySpaces))
            .Plus(TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.BUILD, UnitType.NAVY, navySpaces)));

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeployStep(this, DeployType.BUILD, UnitType.ARMY, armySpaces, "Build an Army in Latin America"),
        TotalWarCards.DeployStep(this, DeployType.BUILD, UnitType.NAVY, navySpaces, "Build a Navy in the South Atlantic or the Southern Ocean")
    };
}
