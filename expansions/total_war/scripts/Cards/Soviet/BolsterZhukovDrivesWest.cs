using System.Collections.Generic;

/// <summary>
/// "Use when you battle Russia, Eastern Europe, Ukraine, or the Balkans. Discard a Build Army card from your
/// hand to build an Army in the space just battled." Offered only when that space can now be built in.
/// </summary>
public partial class BolsterZhukovDrivesWest : BolsterCardLogic
{
    private static readonly List<Country> Fronts = [Country.Russia, Country.EasternEurope, Country.Ukraine, Country.Balkans];

    private List<CountryState> BuildTargets(GameSituation situation) =>
        TriggerContextAs<BattleCountryChangeEvent>(situation)?.CountryState is { } battled && Fronts.Contains(battled.Country)
            ? TotalWarCards.DeployTargets(situation.Board, Faction, DeployType.BUILD, UnitType.ARMY, [battled.Country])
            : new();

    public override TargetSet Targets() => TargetSet.Countries(BuildTargets(GameSituation.Live));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.FactionBattled(Faction).Immediately(), this),
        Condition.Build(new Condition.CustomCondition(s => BuildTargets(s).Count > 0), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.TypedDiscardStep(this, CardType.BUILD_ARMY, "Discard a Build Army card to build an Army in the space just battled",
            s => BuildTargets(s).Count > 0),
        new ResultStep(this, Choose.CountryFrom(c => BuildTargets(c.Situation).ToCountryIds(),
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD)))
            .RequiringPreviousStep()
            .WithGuidance("Build an Army in the space just battled")
    };
}
