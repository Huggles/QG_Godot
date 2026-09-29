using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusSuperiorShipyards : StatusCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.HasDeployedNavy(Faction), this).Immediately(),
            Condition.Build(new Condition.HasBuildableSea(Faction), this)
        };
    }
    
    public List<int> DeployableCountryIds(BoardState board) =>
        board.BuildableSea(Faction).Select(cs => cs.Id).ToList();

    /// <summary>Where the additional Navy may be built — the same list the step offers.</summary>
    public override TargetSet Targets() => TargetSet.Countries(DeployableCountryIds(BoardState.Live));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new RequirementStep(this, Choose.Fixed(_ => new ForceDiscardCardsChangeEvent(Faction, Faction, 1)))
            .WithGuidance("Discard top 1 deck card to build an additional Navy"),

            new ResultStep(this, Choose.CountryFrom(c => DeployableCountryIds(c.Board),
                (countryId, _) => new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD)))
            .RequiringPreviousStep()
        };
    }
}