using System;
using System.Collections.Generic;
using Godot;

public partial class StatusWartimeProduction : StatusCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.FactionDeployed(Faction, DeployType.BUILD).WithUnitType(UnitType.ARMY), this),
            Condition.Build(new Condition.CustomCondition(() => DeployableCountryIds.Count > 0), this)
        };
    }
    
    public List<int> DeployableCountryIds
    {
        get
        {
            return DeployState.CalculateDeployState(Faction).BuildableCountryStatesForType(CountryType.LAND).Map(cs => cs.Id);
        }
    }

    public override List<CardStep> InitializeReactCardSteps()
    {
        //TODO
        return new List<CardStep> {
            new CardStep(this, async() => {
                ;
                int selectedCountryId = await new SelectCountryHandler(DeployableCountryIds).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;                
            }).WithGuidance("Build an army")
        };
    }


}