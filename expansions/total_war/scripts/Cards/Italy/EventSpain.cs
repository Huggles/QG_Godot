using System.Collections.Generic;

public partial class EventSpain : EventCardLogic
{
    private static readonly List<Country> armySpaces = [Country.WesternEurope, Country.NorthAfrica];
    private static readonly List<Country> navySpaces = [Country.MediterraneanSea, Country.NorthSea];

    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.BUILD, UnitType.ARMY, armySpaces))
            .Plus(TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.BUILD, UnitType.NAVY, navySpaces)));

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeployStep(this, DeployType.BUILD, UnitType.ARMY, armySpaces, "Build an Army in Western Europe or North Africa"),
        TotalWarCards.DeployStep(this, DeployType.BUILD, UnitType.NAVY, navySpaces, "Build a Navy in the Mediterranean Sea or the North Sea")
    };
}
