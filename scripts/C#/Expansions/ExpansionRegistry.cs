using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/// <summary>
/// Every expansion this install knows about, and what happened when it was loaded. Filled once at boot,
/// from <see cref="GameManager"/>'s _Ready, before any game data is read — a card's ExecutionClass or a
/// message on the wire may name a type that only an expansion assembly provides.
///
/// An expansion is a folder under <see cref="Root"/> with an <c>expansion.json</c> at its root. Its data
/// and art are addressed by res:// path, which is the same whether the folder is part of the project
/// (editor, CLI, sim) or comes from the DLC's .pck mounted at those paths (an export). Its code, if it
/// has any, is a separate assembly loaded by <see cref="ExpansionLoader"/>.
///
/// Knowing about an expansion is not the same as playing with it: which expansions a session enables
/// is a session setting, decided per game.
/// </summary>
public static class ExpansionRegistry
{
    public const string Root = "res://expansions/";
    public const string ManifestFileName = "expansion.json";

    private static readonly List<LoadedExpansion> _expansions = new();

    public static IReadOnlyList<LoadedExpansion> All => _expansions;

    public static LoadedExpansion Find(string id) => _expansions.Find(e => e.Manifest.Id == id);

    /// <summary>The ids of every expansion that could be enabled for a game on this install.</summary>
    public static List<string> AvailableIds() => _expansions.Where(e => e.IsAvailable).Select(e => e.Manifest.Id).ToList();

    /// <summary>A player-facing name for <paramref name="id"/>, even for an expansion this install lacks.</summary>
    public static string DisplayName(string id) => Find(id)?.Manifest.DisplayName ?? id;

    /// <summary>
    /// Of <paramref name="ids"/>, the ones this install cannot play, each with the reason — empty when
    /// all of them can be. What a session start, a save load and the lobby all check before going on.
    /// </summary>
    public static List<string> Unavailable(IEnumerable<string> ids) => ids
        .Select(id => Find(id) is { } expansion
            ? expansion.UnavailableReason
            : $"{id} is not installed")
        .Where(reason => reason != null)
        .ToList();

    private static bool _discovered;

    /// <summary>Finds every expansion manifest under <see cref="Root"/>. Safe to call more than once.</summary>
    public static void Discover()
    {
        if (_discovered) return;
        _discovered = true;

        using DirAccess dir = DirAccess.Open(Root);
        if (dir == null)
        {
            // The normal case for a base install without any DLC once expansions are exported separately.
            DebugUtilities.PrintPeer("Expansions: none installed");
            return;
        }

        foreach (string folder in dir.GetDirectories())
        {
            string manifestPath = $"{Root}{folder}/{ManifestFileName}";
            if (!FileAccess.FileExists(manifestPath)) continue;

            ExpansionManifest manifest = ExpansionManifest.Read(manifestPath, folder);
            if (manifest == null) continue;

            _expansions.Add(new LoadedExpansion(manifest, $"{Root}{folder}/"));
            DebugUtilities.PrintPeer($"Expansions: found '{manifest.Id}' ({manifest.DisplayName})");
        }
    }
}

/// <summary>One expansion found by <see cref="ExpansionRegistry.Discover"/>, and the outcome of loading it.</summary>
public sealed class LoadedExpansion
{
    public ExpansionManifest Manifest { get; }

    /// <summary>The expansion's folder, e.g. <c>res://expansions/total_war/</c>, with a trailing slash.</summary>
    public string ResFolder { get; }

    /// <summary>Where its assembly was loaded from, or null if it has none or it was not found.</summary>
    public string AssemblyPath { get; internal set; }

    public Assembly Assembly { get; internal set; }

    public List<IExpansionRules> Rules { get; } = new();

    /// <summary>Why this expansion failed to load, or null if it loaded (or had no code to load).</summary>
    public string Error { get; internal set; }

    /// <summary>Its content is here and its code, if it has any, loaded cleanly.</summary>
    public bool IsLoaded => Error == null && (!Manifest.HasAssembly || Assembly != null);

    /// <summary>
    /// The player owns it. Only Steam can say otherwise: with no DLC app id yet, or no Steam to ask (the
    /// editor, the CLI, the sim, a headless server), having the files is enough. Asked each time rather
    /// than cached because Steam initialises after the expansions load (SteamworksApi is a later
    /// autoload), and a purchase can land while the game is running.
    /// </summary>
    public bool IsOwned => Manifest.SteamDlcAppId == 0 || !SteamworksApi.IsAvailable || SteamworksApi.OwnsDlc(Manifest.SteamDlcAppId);

    /// <summary>Can be enabled for a game on this install.</summary>
    public bool IsAvailable => IsLoaded && IsOwned;

    /// <summary>Why it cannot be enabled, in words for a player; null when it can.</summary>
    public string UnavailableReason =>
        !IsLoaded ? $"{Manifest.DisplayName} could not be loaded ({Error ?? "no code"})"
        : !IsOwned ? $"{Manifest.DisplayName} is not owned on this Steam account"
        : null;

    public LoadedExpansion(ExpansionManifest manifest, string resFolder)
    {
        Manifest = manifest;
        ResFolder = resFolder;
    }
}
