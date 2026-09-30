using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusWartimeProduction : StatusCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.HasDeployedArmy(Faction), this).Immediately(),
            Condition.Build(new Condition.HasBuildableLand(Faction), this)
        };
    }
    
    public List<int> DeployableCountryIds(BoardState board) =>
        board.BuildableLand(Faction).Select(cs => cs.Id).ToList();

    /// <summary>Where the additional Army may be built — the same list the step offers.</summary>
    public override TargetSet Targets() => TargetSet.Countries(DeployableCountryIds(BoardState.Live));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new RequirementStep(this, Choose.Fixed(_ => new ForceDiscardCardsChangeEvent(Faction, Faction, 1)))
            .WithGuidance("Discard top 1 deck card to build an additional Army"),

            new ResultStep(this, Choose.CountryFrom(c => DeployableCountryIds(c.Board),
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD)))
            .RequiringPreviousStep()
        };
    }


}