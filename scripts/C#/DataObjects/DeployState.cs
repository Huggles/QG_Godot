using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class DeployState : Node
{

    public Faction Faction;
    public List<int> BuildableCountries = new List<int>();
    public List<CountryState> BuildableCountryStates => BuildableCountries.ToCountryStates();
    public List<CountryState> BuildableCountryStatesForType(CountryType countryType) => BuildableCountryStates.Where(cs=>cs.Type == countryType).ToList();

    public List<int> RecruitableCountries = new List<int>();
    public List<CountryState> RecruitableCountriesStates => RecruitableCountries.ToCountryStates();
    public List<CountryState> RecruitableCountriesStatesForType(CountryType countryType) => RecruitableCountriesStates.Where(cs=>cs.Type == countryType).ToList();    

    public static DeployState CalculateDeployState(Faction faction)
    {
        DeployState deployState = new DeployState();
        MultiplayerGameState GameState = GameSession.Current.GameState;
        
        List<int> suppliedUnitIds = GameAPI.SuppliedUnitsForFaction(faction);
        if (suppliedUnitIds.Count > 0)
        {
            foreach (CountryState countryState in CountryState.ForUnitIds(suppliedUnitIds))
            {
                foreach (CountryState neighbor in countryState.ConnectedCountryStates)
                {
                    if (neighbor.CanBuild(faction))
                    {
                        deployState.BuildableCountries.Add(neighbor.Id);
                    }

                }
            }
        }

        if (FactionState.ForEnum(faction).FactionData.HomeSpaceCountryState.CanBuild(faction)) {
            deployState.BuildableCountries.Add(FactionState.ForEnum(faction).FactionData.HomeSpaceCountryState.Id);
        }

        foreach (CountryState countryState in GameState.CountryStates)
            {
                if (countryState.CanRecruit(faction))
                {
                    deployState.RecruitableCountries.Add(countryState.Id);
                }
            }
        return deployState;
    }
}
