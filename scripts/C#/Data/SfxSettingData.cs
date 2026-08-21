using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// One configurable sound effect from <c>res://assets/audio/sfx/settings.json</c>: a cue the player
/// can pick the sound for. <see cref="Name"/> is the stable key the choice is saved under and the
/// key call sites use — <see cref="Label"/> is display text only and is safe to reword.
/// </summary>
public class SfxSettingData
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; }

    [JsonPropertyName("sound_files")]
    public List<SfxSoundFileData> SoundFiles { get; set; } = new List<SfxSoundFileData>();
}

/// <summary>One selectable sound for an <see cref="SfxSettingData"/>.</summary>
public class SfxSoundFileData
{
    /// <summary>Stable key persisted in the player's settings. Never shown.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Path to the audio file, extension omitted (e.g. <c>assets/audio/sfx/InputRequest1</c>).
    /// Resolved to a clip by filename against the scanned sfx folder, so the folder scan stays the
    /// single source of truth for what is actually loadable.
    /// </summary>
    [JsonPropertyName("file")]
    public string File { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; }
}

/// <summary>Root of <c>settings.json</c>.</summary>
public class SfxSettingsFileData
{
    [JsonPropertyName("settings")]
    public List<SfxSettingData> Settings { get; set; } = new List<SfxSettingData>();
}
