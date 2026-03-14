using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
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
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsBuildable(TargetableCountryStates.ToCountryIds(), Faction),this))
            .WithGuidance("Build a navy")
        }; 
    }
    
    public List<CountryState> TargetableCountryStates
    {
        get
        {
            return CountryState.AllCountryStates.Where(cs => cs.Tags.Has(Tag.Buildable, Faction) && cs.Type == CountryType.SEA).ToList();
        }
    }
}
