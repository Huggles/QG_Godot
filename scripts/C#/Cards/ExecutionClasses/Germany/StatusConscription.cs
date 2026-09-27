using System.Threading.Tasks;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusConscription : StatusCardLogic
{
    public List<int> BuildableLandCountries()
    {
        return CountryState.BuildableLand(Faction).ToCountryIds();
    }

    /// <summary>Where the army may be built — the same list the step offers.</summary>
    public override TargetSet Targets() => TargetSet.Countries(BuildableLandCountries());

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsPlayCardStep(), this),
            Condition.Build(new Condition.IsFactionTurn(Faction), this),
            Condition.Build(new Condition.Not(new Condition.HasPlayedCardThisTurnStep(Faction)), this)
        };
    }

    public override List<CardStep> OnActivate() 
    {
        return new List<CardStep> {
            new RequirementStep(this, Choose.Fixed(() => new SpendPlayActionChangeEvent(Faction)))
            .WithCondition(()=> Condition.Build(new Condition.CountryIsBuildable(BuildableLandCountries(), Faction),this))            
            .WithGuidance("Build an army"),

            new RequirementStep(this, Choose.Fixed(() => new ForceDiscardCardsChangeEvent(Faction, Faction, 2)))
            .RequiringPreviousStep(),

            new ResultStep(this, Choose.CountryFrom(() => BuildableLandCountries(),
                countryId => new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD)))
            .RequiringPreviousStep()
        };
    }
}
