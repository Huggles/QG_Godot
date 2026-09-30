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
    private CountryState TriggerNavyLocation(GameSituation situation) =>
        _triggerNavyLocation ?? TriggerContextAs<DeployUnitChangeEvent>(situation)?.CountryState;

    /// <summary>The land spaces beside that navy that can actually take an Army. Both steps offer
    /// this same list, and so does <see cref="Targets"/>.</summary>
    private List<CountryState> AdjacentBuildable(GameSituation situation) =>
        TriggerNavyLocation(situation) == null
            ? new List<CountryState>()
            : situation.Board.BuildableLand(Faction)
                .Where(cs => TriggerNavyLocation(situation).ConnectedCountryStates.Contains(cs))
                .ToList();

    public override TargetSet Targets() => TargetSet.Countries(AdjacentBuildable(GameSituation.Live));

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
            new ResultStep(this, async() => {
                var triggerDeploy = TriggerContextAs<DeployUnitChangeEvent>();
                if (triggerDeploy == null) return CardStepResult.Nothing;
                _triggerNavyLocation = triggerDeploy.CountryState;
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, AdjacentBuildable(GameSituation.Live).ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD);
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(s => AdjacentBuildable(s).Count > 0), this))
            .WithGuidance("Build an army adjacent to the navy just built"),
            
            // Build second Army adjacent to the same Navy (location captured in step 1)
            new ResultStep(this, async() => {
                if (_triggerNavyLocation == null) return CardStepResult.Nothing;
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, AdjacentBuildable(GameSituation.Live).ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD);
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(s =>
                _triggerNavyLocation != null && AdjacentBuildable(s).Count > 0), this))
            .WithGuidance("Build second army adjacent to the navy just built"),
        }; 
    }
}