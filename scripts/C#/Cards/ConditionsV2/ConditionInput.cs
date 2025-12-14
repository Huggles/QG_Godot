using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public class ConditionInput
{
    public int TurnNumber => GameSession.Instance.GameFlow.GameTurn;
    public TurnStep TurnStep => GameSession.Instance.GameFlow.TurnStep;

    [JsonPropertyName("countries")]
    public List<string> Countries;    
    
    [JsonPropertyName("cards")]
    public List<string> Cards;    

    [JsonPropertyName("faction")]
    public List<string> Factions;

    [JsonPropertyName("targetFactions")]
    public List<string> TargetFactions;

    [JsonPropertyName("unitType")]
    public string UnitType;

    [JsonPropertyName("countryType")]
    public string CountryType;

    [JsonPropertyName("deployType")]
    public string DeployType;


    [JsonIgnore]
    public List<Faction> FactionEnums => Factions.Map(f => Enum.Parse<Faction>(f));
    [JsonIgnore]
    public List<Faction> TargetFactionEnums => TargetFactions.Map(f => Enum.Parse<Faction>(f));
    [JsonIgnore]
    public FactionTeam FactionTeamEnum => Enum.Parse<FactionTeam>(DeployType);
    [JsonIgnore]
    public FactionTeam TargetFactionTeamEnum => Enum.Parse<FactionTeam>(DeployType);
    [JsonIgnore]
    public DeployType DeployTypeEnum => Enum.Parse<DeployType>(DeployType);
    [JsonIgnore]
    public UnitType UnitTypeEnum => Enum.Parse<UnitType>(UnitType);
    [JsonIgnore]
    public CountryType CountryTypeEnum => Enum.Parse<CountryType>(CountryType);

    /*
    * Helpers
    */
    [JsonIgnore]
    public List<CountryState> CountryStates => CountryState.ForNames(Countries);
    [JsonIgnore]
    public List<int> CountryIds => CountryStates.Map(cs => cs.Id);        


    [JsonIgnore]
    public List<CardState> CardStates => CardState.ForNames(Cards);
    
    [JsonIgnore]
    public List<int> CardIds => CardStates.Map(cs => cs.Id);
}
