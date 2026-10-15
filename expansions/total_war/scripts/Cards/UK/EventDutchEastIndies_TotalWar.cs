using System.Collections.Generic;

public partial class EventDutchEastIndies_TotalWar : EventCardLogic
{
    private static readonly List<Country> navySpaces = [Country.SouthChinaSea];

    public override TargetSet Targets() => TargetSet.Countries(new List<Country> { Country.Indonesia, Country.NewGuinea, Country.SouthChinaSea });

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeployStepFor(this, Faction.FRANCE, DeployType.RECRUIT, UnitType.ARMY, [Country.Indonesia], "Recruit a French Army in Indonesia"),
        TotalWarCards.DeployStepFor(this, Faction.FRANCE, DeployType.RECRUIT, UnitType.ARMY, [Country.NewGuinea], "Recruit a French Army in New Guinea"),
        TotalWarCards.DeployStepFor(this, Faction.FRANCE, DeployType.RECRUIT, UnitType.NAVY, navySpaces, "Recruit a French Navy in the South China Sea")
    };
}
