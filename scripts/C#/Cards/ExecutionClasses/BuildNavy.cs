using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class BuildNavy : CardLogic
{    
    public List<CountryState> TargetableCountryStates
    {
        get
        {
            return GameSession.BuildableCountriesForFaction(Faction).ToCountryStates().FindAll(CountryState => CountryState.IsSea);
        }
    }
    public override bool CanPlayCard()
    {      
        return TargetableCountryStates.Count > 0;
    }

    public override async void InitialPlayStep()
    {
        int selectedCountryId = await new SelectCountryHandler(TargetableCountryStates.ToCountryIds()).Handle();
        DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
        deployUnitChangeEvent.IsTrigger = true;
        CardPlayPool.DoChangeEvent(deployUnitChangeEvent);        
    }
}
