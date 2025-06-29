using Godot;
using System;
using System.Collections.Generic;
using System.Data.Common;

public partial class StatusAtlanticWall : StatusCardLogic
{
    protected override List<Condition> CardTriggers()
    {        
        return new List<Condition> {
            Condition.Build(
                new Condition.FactionTeamUnitInCountryIsAttacked(
                    StaticGameData.FactionTeamForFaction(Faction),
                    new List<int> { (int)Country.WesternEurope }
                ),
                this
            ),
        };
    }

    public override List<CardStep> InitializeReactCardSteps()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {                
                int countryId = await new SelectCountryHandler(DeployState.CalculateDeployState(Faction).BuildableCountries).Handle();                
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                CardPlayPool.DoChangeEvent(deployUnitChangeEvent);                
            }).WithGuidance("Deploy an army")
            
        };
    }
}
