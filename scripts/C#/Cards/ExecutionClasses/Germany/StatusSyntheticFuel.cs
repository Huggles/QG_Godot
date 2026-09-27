using System.Threading.Tasks;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusSyntheticFuel : StatusCardLogic
{    
    protected override List<Condition> CardTriggers()
    {        
        return new List<Condition> {
            Condition.Build(new Condition.FactionDeployed(Faction, DeployType.BUILD), this).Immediately(),
            Condition.Build(
                new Condition.CountryIsBuildable(
                    DeployTargets.ToCountryIds(),
                    Faction),
                this
            )
            
        };
    }
    
    public List<CountryState> DeployTargets
    {
        get 
        {
            var trigger = TriggerContextAs<DeployUnitChangeEvent>();
            if (trigger == null) return new List<CountryState>();
            return CountryState.ForId(trigger.CountryId).AdjacentCountryStates(Faction)
                .Where(countryState => countryState.CanBuild(Faction) && countryState.IsLand)
                .Distinct()
                .ToList();
        }
    }

    /// <summary>The spaces adjacent to the deploy that can take the second Army.</summary>
    public override TargetSet Targets() => TargetSet.Countries(DeployTargets);

    public override List<CardStep> OnActivate() 
    {
        return new List<CardStep> {
            new RequirementStep(this, Choose.Fixed(() => new ForceDiscardCardsChangeEvent(Faction, Faction, 2)))
            .WithGuidance("Deploy an army adjacent to where you've deployed an army this turn"),

            new ResultStep(this, Choose.CountryFrom(() => DeployTargets.ToCountryIds(),
                countryId => new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD)))
            .RequiringPreviousStep()
        };
    }
}
