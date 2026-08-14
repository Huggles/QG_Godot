using System.Text.Json.Serialization;

public class ScenarioInfo
{
    [JsonIgnore]
    public string Path { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    /// <summary>
    /// This scenario's answer to InitialGameStateData.OpeningDiscard, mirrored here so the lobby can
    /// show it on the checkbox the moment a scenario is picked — without opening and deserializing
    /// the whole file. Defaults to true, matching the data model.
    /// </summary>
    [JsonPropertyName("openingDiscard")]
    public bool OpeningDiscard { get; set; } = true;
}
