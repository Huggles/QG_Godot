using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusWartimeProduction : StatusCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.HasDeployedArmy(Faction), this),
            Condition.Build(new Condition.HasBuildableLand(Faction), this)
        };
    }
    
    public List<int> DeployableCountryIds
    {
        get
        {
            return CountryState.BuildableLand(Faction).Select(cs => cs.Id).ToList();
        }
    }

    public override List<CardStep> OnActivate()
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