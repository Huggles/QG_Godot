using Godot;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// The <c>expansion.json</c> at the root of an expansion folder (<c>res://expansions/&lt;id&gt;/</c>).
///
/// The folder's id and the manifest's <see cref="Id"/> must match: everything the expansion ships is
/// addressed by that path, in the editor (the folder is part of the project) and in an export alike
/// (the DLC's .pck is mounted at the same res:// paths), so a mismatch would be a manifest pointing
/// at somebody else's content.
/// </summary>
public sealed class ExpansionManifest
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; }

    /// <summary>
    /// Name of the expansion's code assembly, without ".dll" — see <see cref="ExpansionLoader"/>.
    /// Empty for an expansion that is data and art only.
    /// </summary>
    [JsonPropertyName("assembly")]
    public string Assembly { get; set; }

    /// <summary>The Steam DLC app that owns this expansion. 0 until the DLC app exists.</summary>
    [JsonPropertyName("steamDlcAppId")]
    public long SteamDlcAppId { get; set; }

    /// <summary>The oldest game version this expansion's content and code were built for.</summary>
    [JsonPropertyName("minGameVersion")]
    public string MinGameVersion { get; set; }

    /// <summary>Art the main menu shows bottom-left while the expansion is playable, relative to its folder. Optional.</summary>
    [JsonPropertyName("menuBanner")]
    public string MenuBanner { get; set; }

    public bool HasAssembly => !string.IsNullOrEmpty(Assembly);

    /// <summary>
    /// Reads and checks the manifest at <paramref name="path"/>. Returns null, after logging why, if it
    /// cannot be used: one broken expansion must never stop the base game from booting.
    /// </summary>
    public static ExpansionManifest Read(string path, string folderName)
    {
        try
        {
            ExpansionManifest manifest = JsonSerializer.Deserialize<ExpansionManifest>(FileAccess.GetFileAsString(path));
            if (manifest == null || string.IsNullOrEmpty(manifest.Id))
            {
                DebugUtilities.PrintPeerError($"Expansion manifest {path} has no id");
                return null;
            }
            if (manifest.Id != folderName)
            {
                DebugUtilities.PrintPeerError($"Expansion manifest {path} says id '{manifest.Id}', but its folder is '{folderName}'");
                return null;
            }
            return manifest;
        }
        catch (Exception e)
        {
            DebugUtilities.PrintPeerError($"Expansion manifest {path} is unreadable: {e.Message}");
            return null;
        }
    }
}
