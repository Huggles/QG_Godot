using System.Collections.Generic;
using System.Linq;

/// <summary>"Use when you deploy or marshal an Air Force. Discard a Build Army card from your hand to build an Army adjacent to that Air Force."</summary>
public partial class BolsterParatroops : BolsterCardLogic
{
    private List<CountryState> BuildTargets(GameSituation situation)
    {
        if (TotalWarCards.AirForceMovedTo(TriggerContextAs<ChangeEvent>(situation), Faction) is not { } airForceIn) return new();
        List<int> adjacent = situation.Board.AdjacentCountryIds(Faction, airForceIn);
        return situation.Board.BuildableLand(Faction).Where(country => adjacent.Contains(country.Id)).ToList();
    }

    public override TargetSet Targets() => TargetSet.Countries(BuildTargets(GameSituation.Live));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.CustomCondition(s => BuildTargets(s).Count > 0).InReactionWindow(), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.TypedDiscardStep(this, CardType.BUILD_ARMY, "Discard a Build Army card to build an Army next to the Air Force",
            s => BuildTargets(s).Count > 0),
        new ResultStep(this, Choose.CountryFrom(c => BuildTargets(c.Situation).ToCountryIds(),
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD)))
            .RequiringPreviousStep()
            .WithGuidance("Build an Army next to the Air Force")
    };
}
