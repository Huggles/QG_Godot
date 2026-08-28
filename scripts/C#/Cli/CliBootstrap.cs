using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Boots a single-process, terminal-driven game: one peer controlling every faction, no lobby, no
/// clients to wait for. Entered from <c>MainScene</c> when <c>cli=true</c>.
///
/// The one non-obvious line is the <see cref="OfflineMultiplayerPeer"/> assignment, and the whole
/// mode depends on it. With no peer at all Godot's <c>get_unique_id()</c> returns 0, so
/// <c>Multiplayer.IsServer()</c> is FALSE and <c>rpcp</c> fails before it ever reaches the CallLocal
/// dispatch. That silently kills the entire chain: NetworkApi.StartMultiplayerSession returns at its
/// first line, MultiplayerSession.Instance stays null, and GameFlow's TurnStepCounter setter never
/// fires a step handler. (This is why MultiplayerLobby's "Debug Solo" button — which changes scene
/// with no peer — does not work.)
///
/// OfflineMultiplayerPeer gives unique id 1, so IsServer() is true, GetPeers() is empty (so
/// PeerReadinessComponent's barrier expects exactly one peer and fires immediately), CallLocal RPCs
/// run locally, and non-CallLocal broadcasts reach nobody. No socket, no port, no wait.
/// </summary>
public static class CliBootstrap
{
    public static void Start(Node from)
    {
        DebugUtilities.PrintPeer("CLI: bootstrapping single-process game");

        // A save carries its own scenario, so it wins over the scenario= argument outright.
        if (!ResolveSave())
            ResolveScenario(from);

        // See the class comment. Must precede the scene change: GameManager watches for the Game node
        // and starts the readiness barrier as soon as it appears.
        from.Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();

        // One player, every playable faction — the same shape MultiplayerLobby's debug-solo path uses.
        // Every InputRequest then targets peer 1, so IsForCurrentPeer is true and the CLI resolver
        // gets asked for all of them.
        List<PlayerFactionAssignment> assignments = new()
        {
            new PlayerFactionAssignment(1, new List<Faction>(StaticGameData.PlayableFactions))
        };
        from.GetNode<GameManager>("/root/GameManager").SetPendingPlayerFactionAssignments(assignments);

        SceneFlow.ChangeScene(from, SceneFlow.GameScenePath);
    }

    /// <summary>
    /// Honour a <c>load=</c> argument by arming the restore the same way LoadGameScreen does, and report
    /// whether one was armed.
    ///
    /// There is no in-session load: restoring means building a whole new session, which is exactly what
    /// booting with this argument does. It is also the cheapest way to test the feature — pair it with
    /// the `save` verb and a fixed seed and the whole round trip is one shell pipeline.
    /// </summary>
    private static bool ResolveSave()
    {
        string path = CliArgs.Get("load");
        if (string.IsNullOrEmpty(path)) return false;

        SaveGame save = SaveGameService.Load(path);
        if (save == null)
            throw new Exception($"CLI: could not read the save at '{path}'.");

        if (save.Version != SaveGame.CurrentVersion)
            throw new Exception($"CLI: save at '{path}' is version {save.Version}, expected {SaveGame.CurrentVersion}.");

        GameManager.ArmRestore(save);

        DebugUtilities.PrintPeer($"CLI: restoring '{save.DisplayName}' ({save.Events.Count} event(s)) from {path}");
        return true;
    }

    /// <summary>
    /// Resolve the <c>scenario=</c> arg against the scenarios GameManager already discovered, by name
    /// (case-insensitive) or as a literal res:// path. GameManager is an autoload whose _Ready has
    /// already run LoadAvailableScenarios and set a default, so writing PendingScenarioPath here wins.
    /// </summary>
    private static void ResolveScenario(Node from)
    {
        string requested = CliArgs.Get("scenario");
        if (string.IsNullOrEmpty(requested)) return;

        if (requested.StartsWith("res://"))
        {
            GameManager.PendingScenarioPath = requested;
            DebugUtilities.PrintPeer($"CLI: scenario path {requested}");
            return;
        }

        GameManager gameManager = from.GetNode<GameManager>("/root/GameManager");
        ScenarioInfo match = gameManager.AvailableScenarios
            .FirstOrDefault(s => s.Name.Equals(requested, StringComparison.OrdinalIgnoreCase));

        if (match == null)
        {
            // Fail loudly rather than silently running the default scenario: a typo'd scenario name
            // that quietly runs something else is the worst possible outcome for a repro script.
            string known = string.Join(", ", gameManager.AvailableScenarios.Select(s => s.Name));
            throw new Exception($"CLI: unknown scenario '{requested}'. Available: {known}");
        }

        GameManager.PendingScenarioPath = match.Path;
        DebugUtilities.PrintPeer($"CLI: scenario {match.Name} ({match.Path})");
    }
}
