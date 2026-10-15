using System.Collections.Generic;

public partial class EventForeignLegion : EventCardLogic
{
    private static readonly List<Country> legionSpaces =
        [Country.Africa, Country.NorthAfrica, Country.Madagascar, Country.SouthEastAsia, Country.NewGuinea, Country.MiddleEast];

    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction.FRANCE, DeployType.RECRUIT, UnitType.ARMY, legionSpaces));

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeployStepFor(this, Faction.FRANCE, DeployType.RECRUIT, UnitType.ARMY, legionSpaces,
            "Recruit a French Army in Africa, North Africa, Madagascar, South East Asia, New Guinea, or the Middle East")
    };
}
