using System.Collections.Generic;

public partial class EventArmeedeTerre : EventCardLogic
{
    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction.FRANCE, DeployType.BUILD, UnitType.ARMY));

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeployStepFor(this, Faction.FRANCE, DeployType.BUILD, UnitType.ARMY, null, "Build a French Army")
    };
}
