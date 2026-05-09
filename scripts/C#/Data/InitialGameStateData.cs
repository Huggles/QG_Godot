using System.Collections.Generic;
using System.Text.Json.Serialization;

public class InitialGameStateData
{
    [JsonPropertyName("unitDeployments")]
    public List<UnitDeploymentData> UnitDeployments { get; set; } = new List<UnitDeploymentData>();

    [JsonPropertyName("debugStatusCards")]
    public List<string> DebugStatusCards { get; set; } = new List<string>();

    [JsonPropertyName("deployHomespaceUnits")]
    public bool DeployHomespaceUnits { get; set; } = true;

    [JsonPropertyName("skipFactions")]
    public List<string> SkipFactions { get; set; } = new List<string>();
}

public class UnitDeploymentData
{
    [JsonPropertyName("faction")]
    public string Faction { get; set; }

    [JsonPropertyName("countryName")]
    public string CountryName { get; set; }

    [JsonPropertyName("deployType")]
    public string DeployType { get; set; }
}
