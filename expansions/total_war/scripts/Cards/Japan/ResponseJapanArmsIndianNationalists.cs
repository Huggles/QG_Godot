using System.Collections.Generic;

public partial class ResponseJapanArmsIndianNationalists : ResponseCardLogic
{
    private static readonly List<Country> india = [Country.India];

    public override TargetSet Targets() => TargetSet.Countries(india);

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.FactionBattled(Faction).Immediately().WithCountries(india), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeployStep(this, DeployType.BUILD, UnitType.ARMY, india, "Build an Army in India")
    };
}
