using System.Collections.Generic;

public partial class EventFreeFrenchAllies_TotalWar : EventCardLogic
{
    private static readonly List<Country> recruitSpaces = [Country.WesternEurope, Country.Africa, Country.NorthAfrica];

    public override TargetSet Targets() =>
        TargetSet.Countries(TotalWarCards.DeployTargets(BoardState.Live, Faction.FRANCE, DeployType.RECRUIT, UnitType.ARMY, recruitSpaces));

    /// <summary>"1 or 2": the second recruit is optional, skipping its prompt ends the card.</summary>
    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeployStepFor(this, Faction.FRANCE, DeployType.RECRUIT, UnitType.ARMY, recruitSpaces,
            "Recruit a French Army in Western Europe, Africa, or North Africa"),
        TotalWarCards.DeployStepFor(this, Faction.FRANCE, DeployType.RECRUIT, UnitType.ARMY, recruitSpaces,
            "Recruit a second French Army in Western Europe, Africa, or North Africa")
            .RequiringPreviousStep()
    };
}
