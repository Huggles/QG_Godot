using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseSpecialNavalLandingForces : ResponseCardLogic
{
    // Captured when step 1 executes; step 2 reads this since CurrentReactionTrigger
    // is restored before ContinueWithNextSteps runs.
    private CountryState _triggerNavyLocation;

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { 
            Condition.Build(new Condition.HasDeployedNavy(Faction).Immediately(), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Build first Army adjacent to the Navy just built (the triggering event)
            new CardStep(this, async() => {
                var triggerDeploy = CardPlayPool.CurrentReactionTrigger as DeployUnitChangeEvent;
                if (triggerDeploy == null) return null;
                _triggerNavyLocation = triggerDeploy.CountryState;
                var adjacentBuildable = CountryState.BuildableLand(Faction)
                    .Where(cs => _triggerNavyLocation.ConnectedCountryStates.Contains(cs))
                    .ToList();
                int selectedCountryId = await new SelectCountryHandler(adjacentBuildable.ToCountryIds()).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => {
                var triggerDeploy = CardPlayPool.CurrentReactionTrigger as DeployUnitChangeEvent;
                if (triggerDeploy == null) return false;
                return CountryState.BuildableLand(Faction).Any(cs => triggerDeploy.CountryState.ConnectedCountryStates.Contains(cs));
            }), this))
            .WithGuidance("Build an army adjacent to the navy just built"),
            
            // Build second Army adjacent to the same Navy (location captured in step 1)
            new CardStep(this, async() => {
                if (_triggerNavyLocation == null) return null;
                var adjacentBuildable = CountryState.BuildableLand(Faction)
                    .Where(cs => _triggerNavyLocation.ConnectedCountryStates.Contains(cs))
                    .ToList();
                int selectedCountryId = await new SelectCountryHandler(adjacentBuildable.ToCountryIds()).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => {
                if (_triggerNavyLocation == null) return false;
                return CountryState.BuildableLand(Faction).Any(cs => _triggerNavyLocation.ConnectedCountryStates.Contains(cs));
            }), this))
            .WithGuidance("Build second army adjacent to the navy just built"),
        }; 
    }
}