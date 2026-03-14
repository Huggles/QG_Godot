using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class StatusAmphibiousLandings : StatusCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.FactionBattled(Faction).WithCountryType(CountryType.LAND), this),
            Condition.Build(new Condition.CustomCondition(() => DeployableCountryIds.Count > 0), this)
        };
    }
    
    public List<int> DeployableCountryIds
    {
        get
        {
            return GameStateCalculator.GetCachedForFaction(Faction).BuildableCountryStates.Where(cs => cs.Type == CountryType.LAND).Select(cs => cs.Id).ToList();
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