using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusSuperiorShipyards : StatusCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.FactionDeployed(Faction, DeployType.BUILD).WithUnitType(UnitType.NAVY), this),            
            Condition.Build(new Condition.CustomCondition(() => DeployableCountryIds.Count > 0), this)
        };
    }
    
    public List<int> DeployableCountryIds
    {
        get
        {
            return DeployState.CalculateDeployState(Faction).BuildableCountryStatesForType(CountryType.SEA).Map(cs => cs.Id);
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
            }).WithGuidance("Build a navy")
        };
    }
}