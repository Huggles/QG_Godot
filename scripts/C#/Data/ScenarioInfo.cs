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

    /// <summary>
    /// Tutorial script this scenario runs, or null for an ordinary game. Mirrored here for the same
    /// reason as OpeningDiscard: the menus need to tell a tutorial from a normal scenario without
    /// deserializing the whole file. The multiplayer lobby uses it to keep tutorials out of its picker,
    /// and the Skirmish screen to lock its seat grid when one is chosen.
    /// </summary>
    [JsonPropertyName("tutorial")]
    public string TutorialPath { get; set; }

    /// <summary>A scenario that names a tutorial script is a tutorial: single player only.</summary>
    [JsonIgnore]
    public bool IsTutorial => !string.IsNullOrEmpty(TutorialPath);

    /// <summary>
    /// A test fixture rather than a scenario anyone would choose to play — the debug and card-test
    /// setups, and the one-round blitz. Absent means false, so the standard game and the tutorial
    /// stay ordinary without having to say so.
    ///
    /// Carried here beside <see cref="IsTutorial"/> for whoever wants to keep these out of a player's
    /// way; nothing filters on it yet.
    /// </summary>
    [JsonPropertyName("debug")]
    public bool IsDebug { get; set; }
}
