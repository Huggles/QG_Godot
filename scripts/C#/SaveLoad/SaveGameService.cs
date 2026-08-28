using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Reads and writes <see cref="SaveGame"/> files under <c>user://saves/</c>, following the same
/// <c>user://</c> + Godot FileAccess conventions <see cref="GameSettings"/> uses.
///
/// Serialization is System.Text.Json, the same as the multiplayer sync layer — which is the point:
/// the event log in a save is the identical DTO shape that already crosses the wire, so there is one
/// serializer to keep correct rather than two.
///
/// Every method reports and returns a failure value rather than throwing. Losing a save is bad; taking
/// the game down with it while the player is mid-turn is worse.
/// </summary>
public static class SaveGameService
{
    public const string SaveDir = "user://saves/";

    /// <summary>
    /// Indented on purpose. A save is a debugging artifact as much as a player feature — being able to
    /// open one and read the event log is worth the extra bytes, and these files are small.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Writes a save and returns its path, or null on failure.</summary>
    public static string Save(SaveGame save)
    {
        if (!EnsureDir()) return null;

        string path = UniquePathFor(save.SavedAtIso);

        try
        {
            using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                DebugUtilities.PrintPeerError(
                    $"SaveGameService: cannot open {path} for write ({FileAccess.GetOpenError()})");
                return null;
            }

            file.StoreString(JsonSerializer.Serialize(save, JsonOptions));
            DebugUtilities.PrintPeer($"SaveGameService: wrote '{save.DisplayName}' to {path} ({save.Events.Count} event(s))");
            return path;
        }
        catch (Exception e)
        {
            DebugUtilities.PrintPeerError($"SaveGameService: save failed: {e.Message}");
            return null;
        }
    }

    /// <summary>Save headers, newest first. An unreadable file is skipped and logged, never fatal.</summary>
    public static List<SaveGameMeta> ListSaves()
    {
        List<SaveGameMeta> result = new();
        if (!EnsureDir()) return result;

        using DirAccess dir = DirAccess.Open(SaveDir);
        if (dir == null)
        {
            DebugUtilities.PrintPeerError($"SaveGameService: cannot open {SaveDir} ({DirAccess.GetOpenError()})");
            return result;
        }

        foreach (string fileName in dir.GetFiles())
        {
            if (!fileName.EndsWith(".json")) continue;

            string path = SaveDir + fileName;
            SaveGame save = Load(path, quiet: true);
            if (save == null) continue;

            result.Add(new SaveGameMeta
            {
                FilePath      = path,
                DisplayName   = save.DisplayName,
                SavedAtIso    = save.SavedAtIso,
                ScenarioTitle = save.ScenarioTitle,
                Version       = save.Version,
                EventCount    = save.Events.Count
            });
        }

        // ISO-8601 sorts lexicographically, so an ordinal compare is a date compare.
        result.Sort((a, b) => string.CompareOrdinal(b.SavedAtIso ?? "", a.SavedAtIso ?? ""));
        return result;
    }

    /// <summary>Loads a full save, or null if it cannot be read or parsed.</summary>
    public static SaveGame Load(string path, bool quiet = false)
    {
        try
        {
            using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                if (!quiet)
                    DebugUtilities.PrintPeerError($"SaveGameService: cannot open {path} ({FileAccess.GetOpenError()})");
                return null;
            }

            return JsonSerializer.Deserialize<SaveGame>(file.GetAsText());
        }
        catch (Exception e)
        {
            // Quiet during a directory listing: one corrupt file must not spam the log with a stack
            // trace per refresh, and the row simply does not appear.
            if (!quiet)
                DebugUtilities.PrintPeerError($"SaveGameService: load failed for {path}: {e.Message}");
            else
                DebugUtilities.PrintPeerFinest($"SaveGameService: skipping unreadable save {path}: {e.Message}");
            return null;
        }
    }

    public static bool Delete(string path)
    {
        if (!FileAccess.FileExists(path)) return false;

        Error error = DirAccess.RemoveAbsolute(path);
        if (error != Error.Ok)
        {
            DebugUtilities.PrintPeerError($"SaveGameService: could not delete {path} ({error})");
            return false;
        }

        DebugUtilities.PrintPeer($"SaveGameService: deleted {path}");
        return true;
    }

    private static bool EnsureDir()
    {
        if (DirAccess.DirExistsAbsolute(SaveDir)) return true;

        Error error = DirAccess.MakeDirRecursiveAbsolute(SaveDir);
        if (error == Error.Ok) return true;

        DebugUtilities.PrintPeerError($"SaveGameService: could not create {SaveDir} ({error})");
        return false;
    }

    /// <summary>
    /// A path that does not exist yet. The timestamp is only second-resolution, so two saves taken in
    /// the same second would otherwise silently overwrite each other.
    /// </summary>
    private static string UniquePathFor(string savedAtIso)
    {
        string stem = SaveDir + $"save_{Sanitize(savedAtIso)}";
        string path = stem + ".json";

        for (int suffix = 2; FileAccess.FileExists(path); suffix++)
            path = $"{stem}_{suffix}.json";

        return path;
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value)) return "unknown";

        foreach (char invalid in new[] { ':', ' ', '/', '\\', 'T', '.' })
            value = value.Replace(invalid.ToString(), "-");

        return value;
    }
}
