using System.Collections.Generic;
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
    /// <see cref="Title"/> in the player's language: the key <c>SCENARIO_&lt;NAME&gt;_TITLE</c> (the file
    /// name, upper-cased), falling back to the file's own English. For anything shown on screen;
    /// <see cref="Title"/> stays English for save names and logs.
    /// </summary>
    [JsonIgnore]
    public string DisplayTitle => Localization.TranslateOr(KeyPrefix + "_TITLE", Title);

    /// <summary><see cref="Description"/> in the player's language, the same way as <see cref="DisplayTitle"/>.</summary>
    [JsonIgnore]
    public string DisplayDescription => Localization.TranslateOr(KeyPrefix + "_DESCRIPTION", Description);

    /// <summary>Scenario_Basic → SCENARIO_BASIC, Tutorial_01 → SCENARIO_TUTORIAL_01.</summary>
    private string KeyPrefix
    {
        get
        {
            string name = Name ?? "";
            if (name.StartsWith("Scenario_")) name = name.Substring("Scenario_".Length);
            return "SCENARIO_" + name.ToUpperInvariant();
        }
    }

    /// <summary>
    /// This scenario's answer to InitialGameStateData.OpeningDiscard, mirrored here so the lobby can
    /// show it on the checkbox the moment a scenario is picked — without opening and deserializing
    /// the whole file. Defaults to true, matching the data model.
    /// </summary>
    [JsonPropertyName("openingDiscard")]
    public bool OpeningDiscard { get; set; } = true;

    /// <summary>
    /// The expansions this scenario is meant to be played with (its optional "expansions" array), which
    /// the menus tick by default when it is picked. Empty for the base game.
    /// </summary>
    [JsonPropertyName("expansions")]
    public List<string> Expansions { get; set; } = new();

    /// <summary>
    /// Tutorial script this scenario runs, or null for an ordinary game. Mirrored here for the same
    /// reason as OpeningDiscard: the menus need to tell a tutorial from a normal scenario without
    /// deserializing the whole file. The Tutorials menu lists exactly these, and the Skirmish picker
    /// leaves them out.
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
