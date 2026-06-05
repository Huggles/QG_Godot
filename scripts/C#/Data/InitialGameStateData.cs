using System.Collections.Generic;
using System.Text.Json.Serialization;

public class InitialGameStateData
{
    [JsonPropertyName("factions")]
    public Dictionary<string, FactionScenarioData> Factions { get; set; } = new Dictionary<string, FactionScenarioData>();

    [JsonPropertyName("startingFaction")]
    public string StartingFaction { get; set; } = null;
}

public class FactionScenarioData
{
    [JsonPropertyName("unitDeployments")]
    public List<UnitDeploymentData> UnitDeployments { get; set; } = new List<UnitDeploymentData>();

    [JsonPropertyName("initialCards")]
    public List<InitialCardEntry> InitialCards { get; set; } = new List<InitialCardEntry>();

    [JsonPropertyName("initialHandCards")]
    public List<InitialHandCardEntry> InitialHandCards { get; set; } = new List<InitialHandCardEntry>();
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
