using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class BuildNavy : CardLogic
{
    
    public override List<CardStep> InitializePlayCardSteps()
    {
        return new List<CardStep> {
            new DeployUnitCardStep(this, async() => {
                int selectedCountryId = await new SelectCountryHandler(TargetableCountryStates.ToCountryIds()).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                _ = CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsBuildable(TargetableCountryStates.ToCountryIds(), Faction),this))
            .WithGuidance("Build a navy")
        }; 
    }
    
    public List<CountryState> TargetableCountryStates
    {
        get
        {
            return DeployState.CalculateDeployState(Faction).BuildableCountryStatesForType(CountryType.SEA);
        }
    }
}
