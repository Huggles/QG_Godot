using System.Collections.Generic;

public partial class EventLaRoyale : EventCardLogic
{
    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction.FRANCE, DeployType.BUILD, UnitType.NAVY));

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeployStepFor(this, Faction.FRANCE, DeployType.BUILD, UnitType.NAVY, null, "Build a French Navy")
    };
}
