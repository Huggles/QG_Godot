using System.Collections.Generic;
using System.Linq;

/// <summary>
/// "Use when you build a Navy in a space with 'Pacific' in the title. Discard the top 1 card of your draw
/// deck to build an Army or Navy in an adjacent space." Offered after the US's own Navy build there.
/// </summary>
public partial class BolsterAGreatSleepingGiant : BolsterCardLogic
{
    /// <summary>The spaces next to the Navy just built where the US can build: an Army on land, a Navy at sea.</summary>
    private List<CountryState> BuildTargets(GameSituation situation)
    {
        if (TriggerContextAs<DeployUnitChangeEvent>(situation)?.CountryState is not { } navyIn || !TotalWarCards.IsPacificSea(navyIn))
            return new();
        List<int> adjacent = situation.Board.AdjacentCountryIds(Faction, navyIn);
        return situation.Board.BuildableLand(Faction).Concat(situation.Board.BuildableSea(Faction))
            .Where(country => adjacent.Contains(country.Id)).ToList();
    }

    public override TargetSet Targets() => TargetSet.Countries(BuildTargets(GameSituation.Live));

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.HasDeployedNavy(Faction).Immediately(), this),
        Condition.Build(new Condition.CustomCondition(s => BuildTargets(s).Count > 0), this)
    };

    public override List<CardStep> OnActivate() => new()
    {
        TotalWarCards.DeckDiscardStep(this, 1, "Discard the top card of your draw deck to build an Army or Navy next to the Navy just built",
            s => BuildTargets(s).Count > 0),
        new ResultStep(this, Choose.CountryFrom(c => BuildTargets(c.Situation).ToCountryIds(),
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD)))
            .RequiringPreviousStep()
            .WithGuidance("Build an Army or Navy next to the Navy just built")
    };
}
