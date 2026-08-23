using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BuildArmy : CardLogic
{  
    public override List<CardStep> OnActivate()
    {
        // Hoisted so the step's own selection and the hover preview cannot drift apart — see
        // CardStep.WithTargetPreview.
        Func<List<int>> buildTargets = () => CountryState.BuildableLand(Faction).ToCountryIds();

        return new List<CardStep> {
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, buildTargets()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithTargetPreview(()=> StepTargetPreview.Countries(buildTargets()))
            .WithCondition(()=> Condition.Build(new Condition.HasBuildableLand(Faction), this))
            .WithAdvisoryCondition(()=> Condition.Build(new Condition.HasVacantBuildableLand(Faction), this))
            .WithGuidance("Build an army")
        }; 
    }
}
