using System.Collections.Generic;

public partial class EventTurkeyJoinsAxis : EventCardLogic
{
    private static readonly List<Country> blackSea = [Country.BlackSea];
    private static readonly List<Country> middleEast = [Country.MiddleEast];

    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.BUILD, UnitType.NAVY, blackSea))
            .Plus(TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction, DeployType.RECRUIT, UnitType.ARMY, middleEast)));

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeployStep(this, DeployType.BUILD, UnitType.NAVY, blackSea, "Build a Navy in the Black Sea"),
        TotalWarCards.DeployStep(this, DeployType.RECRUIT, UnitType.ARMY, middleEast, "Recruit an Army in the Middle East")
    };
}
