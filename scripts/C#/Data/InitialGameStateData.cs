using System.Collections.Generic;
using System.Text.Json.Serialization;

public class InitialGameStateData
{
    [JsonPropertyName("unitDeployments")]
    public List<UnitDeploymentData> UnitDeployments { get; set; } = new List<UnitDeploymentData>();

    [JsonPropertyName("initialCards")]
    public List<InitialCardEntry> InitialCards { get; set; } = new List<InitialCardEntry>();

    [JsonPropertyName("startingFaction")]
    public string StartingFaction { get; set; } = null;
}

public class InitialCardEntry
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

}

public class UnitDeploymentData
{
    [JsonPropertyName("faction")]
    public string Faction { get; set; }

    [JsonPropertyName("countryName")]
    public string CountryName { get; set; }

}
