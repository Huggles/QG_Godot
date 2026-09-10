using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Central registry for managing player-to-faction mappings in multiplayer games.
/// This is the single source of truth for routing input and UI to the correct player.
/// 
/// Architecture: Host-Authoritative
/// - Host (peer_id=1) runs all game logic
/// - Clients send input choices to host via RPC
/// - Host validates and broadcasts state changes
/// 
/// Usage:
/// 1. RegisterPlayer() for each player that joins
/// 2. AssignFactionsToPlayer() to set which factions each player controls
/// 3. Query methods to route input/UI to correct player
/// </summary>
public static class PlayerFactionRegistry
{
    private static Dictionary<Faction, int> _factionToPeerId = new Dictionary<Faction, int>();
    private static Dictionary<int, PlayerScene> _peerIdToPlayerScene = new Dictionary<int, PlayerScene>();
    private static Dictionary<int, List<Faction>> _peerIdToFactions = new Dictionary<int, List<Faction>>();

    /// <summary>
    /// First id handed to an AI seat. An AI seat is an ordinary <see cref="PlayerScene"/> registered
    /// under an id in this range, which is what makes it invisible with no special cases:
    ///
    ///  - PlayerScene._Ready only assigns <see cref="PlayerScene.Current"/> when the local peer id
    ///    equals the node's authority, and no real peer is ever numbered from here — so an AI seat
    ///    never becomes Current on ANY peer. Everything that renders "my factions" reads Current, so
    ///    the bot's hand and its face-down Response cards stay off the human's screen for free.
    ///  - NetworkApi.LoadPlayers names the node Player_{peerId}, which is the RPC NodePath, so the id
    ///    must be unique and identical on every peer. Every peer deserialises the same assignment
    ///    list, so it is.
    ///  - RegisterPlayer rejects a duplicate id, so an AI seat cannot share one with the host.
    ///
    /// Godot peer ids are positive and the host is 1, so a high base cannot collide. Deliberately not
    /// a negative sentinel: -1 already means "no card"/"no unit" in several places, and a negative
    /// here would read as an error rather than as a seat.
    /// </summary>
    public const int AiSeatIdBase = 1000;

    /// <summary>Whether this registry id belongs to an AI seat rather than a network peer.</summary>
    public static bool IsAiSeatId(int peerId) => peerId >= AiSeatIdBase;


    /// <summary>
    /// Check if we're in single player mode (one player controls all factions)
    /// </summary>
    public static bool IsSinglePlayer => GetPlayerCount() == 1;
    
    /// <summary>
    /// Check if we're in any multiplayer mode
    /// </summary>
    public static bool IsMultiplayer => GetPlayerCount() > 1;

    // ==================== REGISTRATION METHODS ====================

    /// <summary>
    /// Register a player (without faction assignment)
    /// Players join first, factions assigned later
    /// </summary>
    public static void RegisterPlayer(PlayerScene playerScene)
    {
        int peerId = playerScene.GetMultiplayerAuthority();
        
        if (_peerIdToPlayerScene.ContainsKey(peerId))
        {
            DebugUtilities.PrintPeerError($"PlayerFactionRegistry: Player with peer_id {peerId} already registered");
            return;
        }

        _peerIdToPlayerScene[peerId] = playerScene;
        _peerIdToFactions[peerId] = new List<Faction>();
        
        DebugUtilities.PrintPeer($"PlayerFactionRegistry: Registered player '{playerScene.PlayerName}' (Peer {peerId})");
    }

    /// <summary>
    /// Assign factions to a player (can be called multiple times to reassign)
    /// </summary>
    public static void AssignFactionsToPlayer(int peerId, List<Faction> factions)
    {
        if (!_peerIdToPlayerScene.ContainsKey(peerId))
        {
            DebugUtilities.PrintPeerError($"PlayerFactionRegistry: Cannot assign factions - peer_id {peerId} not registered");
            return;
        }

        // Remove old faction assignments for this peer
        if (_peerIdToFactions.ContainsKey(peerId))
        {
            foreach (Faction oldFaction in _peerIdToFactions[peerId])
            {
                _factionToPeerId.Remove(oldFaction);
            }
        }

        // Assign new factions
        _peerIdToFactions[peerId] = new List<Faction>(factions);
        foreach (Faction faction in factions)
        {
            _factionToPeerId[faction] = peerId;
        }

        // Update the player scene
        PlayerScene playerScene = _peerIdToPlayerScene[peerId];
        playerScene.SetControlledFactions(factions);
        
        DebugUtilities.PrintPeer($"PlayerFactionRegistry: Assigned factions to '{playerScene.PlayerName}' (Peer {peerId}): {string.Join(", ", factions)}");
    }

    /// <summary>
    /// Unregister a player (when they leave)
    /// </summary>
    public static void UnregisterPlayer(int peerId)
    {
        if (!_peerIdToPlayerScene.ContainsKey(peerId))
        {
            return;
        }

        // Remove faction assignments
        if (_peerIdToFactions.ContainsKey(peerId))
        {
            foreach (Faction faction in _peerIdToFactions[peerId])
            {
                _factionToPeerId.Remove(faction);
            }
            _peerIdToFactions.Remove(peerId);
        }

        string playerName = _peerIdToPlayerScene[peerId].PlayerName;
        _peerIdToPlayerScene.Remove(peerId);
        
        DebugUtilities.PrintPeer($"PlayerFactionRegistry: Unregistered player '{playerName}' (Peer {peerId})");
    }

    /// <summary>
    /// Clear all registrations
    /// </summary>
    public static void Clear()
    {
        _factionToPeerId.Clear();
        _peerIdToPlayerScene.Clear();
        _peerIdToFactions.Clear();
        DebugUtilities.PrintPeer("PlayerFactionRegistry: Cleared all registrations");
    }

    // ==================== QUERY METHODS ====================

    /// <summary>
    /// Get the peer ID that controls a specific faction
    /// </summary>
    public static int GetPeerIdForFaction(Faction faction)
    {
        if (_factionToPeerId.TryGetValue(faction, out int peerId))
        {
            return peerId;
        }
        
        DebugUtilities.PrintPeerError($"PlayerFactionRegistry: No peer found for faction {faction}");
        return 1; // Default to host
    }

    /// <summary>
    /// The display name of the player controlling <paramref name="faction"/>, or null when nobody does
    /// or they have no name.
    ///
    /// Deliberately NOT routed through <see cref="GetPeerIdForFaction"/>: that logs an error and falls
    /// back to the host on a miss, which is right for input routing and wrong for a label — a history
    /// row for Faction.NONE would spam the error log and then credit the host with it.
    /// </summary>
    public static string GetDisplayNameForFaction(Faction faction)
    {
        if (!_factionToPeerId.TryGetValue(faction, out int peerId)) return null;
        if (!_peerIdToPlayerScene.TryGetValue(peerId, out PlayerScene playerScene)) return null;
        if (!GodotObject.IsInstanceValid(playerScene)) return null;
        return string.IsNullOrWhiteSpace(playerScene.DisplayName) ? null : playerScene.DisplayName;
    }

    /// <summary>
    /// Get the PlayerScene that controls a specific faction
    /// </summary>
    public static PlayerScene GetPlayerSceneForFaction(Faction faction)
    {
        int peerId = GetPeerIdForFaction(faction);
        if (_peerIdToPlayerScene.TryGetValue(peerId, out PlayerScene playerScene)
            && GodotObject.IsInstanceValid(playerScene))
        {
            return playerScene;
        }
        
        DebugUtilities.PrintPeerError($"PlayerFactionRegistry: No PlayerScene found for faction {faction}");
        return null;
    }

    /// <summary>
    /// Get the InputManager for a specific faction (for input routing)
    /// </summary>
    public static InputManager GetInputManagerForFaction(Faction faction)
    {
        PlayerScene playerScene = GetPlayerSceneForFaction(faction);
        return playerScene?.InputManager;
    }

    /// <summary>
    /// Get all factions controlled by a specific peer
    /// </summary>
    public static List<Faction> GetFactionsForPeerId(int peerId)
    {
        if (_peerIdToFactions.TryGetValue(peerId, out List<Faction> factions))
        {
            return new List<Faction>(factions);
        }
        return new List<Faction>();
    }

    /// <summary>
    /// Get all factions controlled by the local player
    /// </summary>
    public static List<Faction> GetLocalPlayerFactions()
    {
        int localPeerId = GetLocalPeerId();
        return GetFactionsForPeerId(localPeerId);
    }

    /// <summary>
    /// Whether this faction is played by a bot rather than a person.
    ///
    /// Deliberately NOT routed through <see cref="GetPeerIdForFaction"/>, for the same reason
    /// <see cref="GetDisplayNameForFaction"/> is not: that logs an error and falls back to the host on
    /// a miss, which is right for input routing and wrong for a question about a label. An unregistered
    /// faction is not an AI faction.
    ///
    /// Fails toward "human" on purpose. Every consumer treats false as "a person is answering", and
    /// wrongly handing a person's seat to a bot is far worse than wrongly showing a bot a chime.
    /// </summary>
    public static bool IsFactionAi(Faction faction)
        => _factionToPeerId.TryGetValue(faction, out int peerId) && IsAiSeatId(peerId);

    /// <summary>
    /// The peer that will actually ANSWER for this faction — as opposed to
    /// <see cref="GetPeerIdForFaction"/>, which returns the registry id of the seat that owns it.
    ///
    /// The two differ only for an AI seat, whose id is synthetic and matches no live peer. Its prompts
    /// are resolved inside the host's process, so the host is the answering peer.
    ///
    /// This is the single place that translation happens, and it needs to stay that way: every
    /// consumer downstream of <see cref="InputRequest.TargetPeer"/> — IsForCurrentPeer, the RpcId
    /// targets in AbortRemoteInput, PendingInput.Peer, SafeTargetPeer — is then correct with no
    /// knowledge of AI seats at all. Letting a synthetic id past here means special-casing it in every
    /// one of them.
    /// </summary>
    public static int GetAnsweringPeerForFaction(Faction faction)
    {
        int peerId = GetPeerIdForFaction(faction);
        return IsAiSeatId(peerId) ? HostPeerId : peerId;
    }

    /// <summary>
    /// The local player's own factions, excluding any AI seat that happens to be hosted here.
    ///
    /// What the HUD wants wherever it means "these are mine": the seat list, the hand buttons, the
    /// opening music. <see cref="GetLocalPlayerFactions"/> answers the different question "which seats
    /// does this peer hold", which for the host includes every bot.
    /// </summary>
    public static List<Faction> GetLocalHumanFactions()
        => GetLocalPlayerFactions().Where(f => !IsFactionAi(f)).ToList();

    /// <summary>Every faction played by a bot, in registry order.</summary>
    public static List<Faction> GetAiFactions()
        => _factionToPeerId.Where(entry => IsAiSeatId(entry.Value)).Select(entry => entry.Key).ToList();

    /// <summary>The host, which is also the peer that answers for every AI seat.</summary>
    public const int HostPeerId = 1;

    /// <summary>
    /// Check if a specific peer can control a faction
    /// </summary>
    public static bool CanPeerControlFaction(int peerId, Faction faction)
    {
        return GetPeerIdForFaction(faction) == peerId;
    }

    /// <summary>
    /// Check if the local player controls a specific faction
    /// </summary>
    public static bool IsLocalPlayerFaction(Faction faction)
    {
        int localPeerId = GetLocalPeerId();
        return CanPeerControlFaction(localPeerId, faction);
    }

    /// <summary>
    /// Check if the local player controls any faction in the given team
    /// </summary>
    public static bool IsLocalPlayerTeam(FactionTeam team)
    {
        List<Faction> localFactions = GetLocalPlayerFactions();
        return localFactions.Any(f => StaticGameData.FactionTeamForFaction(f) == team);
    }

    /// <summary>
    /// Get the local player's peer ID
    /// </summary>
    public static int GetLocalPeerId()
    {
        // In single player, always return 1
        if (IsSinglePlayer)
            return 1;
        
        // In multiplayer, get from Godot's multiplayer API
        // Access through Engine rather than SceneTree
        var mainLoop = Engine.GetMainLoop();
        if (mainLoop is SceneTree tree)
        {
            return tree.GetMultiplayer().GetUniqueId();
        }
        
        return 1; // Default to host if multiplayer not set up
    }

    /// <summary>
    /// Check if the local player is the host (peer_id = 1)
    /// </summary>
    public static bool IsLocalPlayerHost()
    {
        return GetLocalPeerId() == 1;
    }

    /// <summary>
    /// Get all registered players
    /// </summary>
    public static List<PlayerScene> GetAllPlayers()
    {
        // Filtered, not raw: this dictionary is static and outlives the game scene, so between one game
        // ending and the next LoadPlayers clearing it, every entry is a freed node whose C# wrapper is
        // still non-null and throws ObjectDisposedException on any read.
        return _peerIdToPlayerScene.Values.Where(GodotObject.IsInstanceValid).ToList();
    }

    /// <summary>
    /// Get count of registered players
    /// </summary>
    public static int GetPlayerCount()
    {
        return _peerIdToPlayerScene.Count;
    }

    // ==================== DEBUG ====================

    /// <summary>
    /// Print current registry state for debugging
    /// </summary>
    public static void PrintStatus()
    {
        DebugUtilities.PrintPeer("=== PlayerFactionRegistry Status ===");
        DebugUtilities.PrintPeer($"Mode: {(IsSinglePlayer ? "Single Player" : $"Multiplayer ({GetPlayerCount()} players)")}");
        DebugUtilities.PrintPeer($"Players: {GetPlayerCount()}");
        
        foreach (var kvp in _peerIdToFactions.OrderBy(k => k.Key))
        {
            string playerName = _peerIdToPlayerScene.TryGetValue(kvp.Key, out PlayerScene ps) 
                ? ps.PlayerName 
                : "Unknown";
            string factionsStr = kvp.Value.Count > 0 
                ? string.Join(", ", kvp.Value) 
                : "No factions assigned";
            DebugUtilities.PrintPeer($"  Peer {kvp.Key} ({playerName}): {factionsStr}");
        }
        
        DebugUtilities.PrintPeer($"Local Peer: {GetLocalPeerId()}");
        DebugUtilities.PrintPeer($"Is Host: {IsLocalPlayerHost()}");
        DebugUtilities.PrintPeer("===================================");
    }
}
