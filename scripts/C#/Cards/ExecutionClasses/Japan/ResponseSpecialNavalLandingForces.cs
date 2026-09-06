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

    /// <summary>
    /// The space the navy was built in. Step 1 caches it into the field above because
    /// CurrentReactionTrigger is restored before step 2 runs; at hover time nothing is cached yet, so
    /// this falls back to the live trigger — which is precisely what the preview needs.
    /// </summary>
    private CountryState TriggerNavyLocation =>
        _triggerNavyLocation ?? TriggerContextAs<DeployUnitChangeEvent>()?.CountryState;

    /// <summary>The land spaces beside that navy that can actually take an Army. Both steps offer
    /// this same list, and so does <see cref="Targets"/>.</summary>
    private List<CountryState> AdjacentBuildable =>
        TriggerNavyLocation == null
            ? new List<CountryState>()
            : CountryState.BuildableLand(Faction)
                .Where(cs => TriggerNavyLocation.ConnectedCountryStates.Contains(cs))
                .ToList();

    public override TargetSet Targets() => TargetSet.Countries(AdjacentBuildable);

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
                var triggerDeploy = TriggerContextAs<DeployUnitChangeEvent>();
                if (triggerDeploy == null) return;
                _triggerNavyLocation = triggerDeploy.CountryState;
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, AdjacentBuildable.ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => AdjacentBuildable.Count > 0), this))
            .WithGuidance("Build an army adjacent to the navy just built"),
            
            // Build second Army adjacent to the same Navy (location captured in step 1)
            new CardStep(this, async() => {
                if (_triggerNavyLocation == null) return;
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, AdjacentBuildable.ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() =>
                _triggerNavyLocation != null && AdjacentBuildable.Count > 0), this))
            .WithGuidance("Build second army adjacent to the navy just built"),
        }; 
    }
}