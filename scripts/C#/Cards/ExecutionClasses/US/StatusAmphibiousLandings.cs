using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusAmphibiousLandings : StatusCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.HasBattledOnLand(Faction), this),
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
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, DeployableCountryIds).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;                
            }).WithGuidance("Build an army")
        };
    }
}