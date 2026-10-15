using System.Collections.Generic;

public partial class EventOperationHerkules : EventCardLogic
{
    private static readonly List<Country> mediterranean = [Country.MediterraneanSea];

    public override TargetSet Targets() => TargetSet.Countries(mediterranean);

    /// <summary>Two recruits in the same sea, one per country: an Italian Navy, then a German one.</summary>
    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeployStepFor(this, Faction.ITALY, DeployType.RECRUIT, UnitType.NAVY, mediterranean, "Recruit an Italian Navy in the Mediterranean Sea"),
        TotalWarCards.DeployStepFor(this, Faction.GERMANY, DeployType.RECRUIT, UnitType.NAVY, mediterranean, "Recruit a German Navy in the Mediterranean Sea")
    };
}
