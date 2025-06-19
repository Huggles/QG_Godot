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
                CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
        }; 
    }
    
    public List<CountryState> TargetableCountryStates
    {
        get
        {
            return GameSession.BuildableCountriesForFaction(Faction).ToCountryStates().FindAll(CountryState => CountryState.IsSea);
        }
    }    

    public override bool CanPlayCard()
    {
        return base.CanPlayCard() && TargetableCountryStates.Count > 0;
    }
}
