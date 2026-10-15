using System.Collections.Generic;
using System.Linq;

public partial class StatusArtificialHarbors : StatusCardLogic
{
    /// <summary>The land spaces next to the Navy just built where the Army can go.</summary>
    private List<CountryState> BuildTargets(GameSituation situation)
    {
        CountryState navyIn = TriggerContextAs<DeployUnitChangeEvent>(situation)?.CountryState;
        if (navyIn == null) return new();
        List<int> adjacent = situation.Board.AdjacentCountryIds(Faction, navyIn);
        return situation.Board.BuildableLand(Faction).Where(country => adjacent.Contains(country.Id)).ToList();
    }

    public override TargetSet Targets() => TargetSet.Countries(BuildTargets(GameSituation.Live));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.HasDeployedNavy(Faction).Immediately(), this),
        Condition.Build(new Condition.CustomCondition(s => BuildTargets(s).Count > 0), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        new RequirementStep(this, Choose.Fixed(_ => new ForceDiscardCardsChangeEvent(Faction, Faction, 1)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => BuildTargets(s).Count > 0), this))
            .WithGuidance("Discard the top card of your draw deck to build an Army adjacent to the Navy just built"),

        new ResultStep(this, Choose.CountryFrom(c => BuildTargets(c.Situation).ToCountryIds(),
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD)))
            .RequiringPreviousStep()
    };
}
