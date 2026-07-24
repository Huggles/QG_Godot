using System.Collections.Generic;
using System.Text.Json.Serialization;

public class InitialGameStateData
{
    [JsonPropertyName("factions")]
    public Dictionary<string, FactionScenarioData> Factions { get; set; } = new Dictionary<string, FactionScenarioData>();

    [JsonPropertyName("startingFaction")]
    public string StartingFaction { get; set; } = null;

    [JsonPropertyName("startingRound")]
    public int StartingRound { get; set; } = 1;

    [JsonPropertyName("maxRounds")]
    public int MaxRounds { get; set; } = 20;

    // When true, every playable faction is given a random starting VP in
    // [RandomStartingVPMin, RandomStartingVPMax], overriding per-faction startingVP.
    [JsonPropertyName("randomizeStartingVP")]
    public bool RandomizeStartingVP { get; set; } = false;

    [JsonPropertyName("randomStartingVPMin")]
    public int RandomStartingVPMin { get; set; } = 0;

    [JsonPropertyName("randomStartingVPMax")]
    public int RandomStartingVPMax { get; set; } = 20;
}

public class FactionScenarioData
{
    [JsonPropertyName("unitDeployments")]
    public List<UnitDeploymentData> UnitDeployments { get; set; } = new List<UnitDeploymentData>();

    [JsonPropertyName("initialCards")]
    public List<InitialCardEntry> InitialCards { get; set; } = new List<InitialCardEntry>();

    [JsonPropertyName("initialHandCards")]
    public List<InitialHandCardEntry> InitialHandCards { get; set; } = new List<InitialHandCardEntry>();

    [JsonPropertyName("startingVP")]
    public int StartingVictoryPoints { get; set; } = 0;
}

public class InitialCardEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
}

public class InitialHandCardEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
}

public class UnitDeploymentData
{
    [JsonPropertyName("countryName")]
    public string CountryName { get; set; }
}
