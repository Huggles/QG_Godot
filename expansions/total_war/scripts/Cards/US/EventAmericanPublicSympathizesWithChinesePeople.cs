using System.Collections.Generic;

public partial class EventAmericanPublicSympathizesWithChinesePeople : EventCardLogic
{
    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction.CHINA, DeployType.BUILD, UnitType.ARMY));

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeployStepFor(this, Faction.CHINA, DeployType.BUILD, UnitType.ARMY, null, "Build a Chinese Army")
    };
}
