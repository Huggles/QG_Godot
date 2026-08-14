using System.Collections.Generic;

/// <summary>Maps a network peer ID to the factions they control in the game.</summary>
/// <param name="DisplayName">
/// The human name to show next to this peer's factions, or null when there is none — single player,
/// CLI, a dedicated server's own peer, or a client whose name never reached the host. Optional so the
/// three single-player construction sites keep their two-argument form: a game with one peer has
/// nobody to name.
///
/// This is the only channel by which a lobby name reaches the game. NetworkApi.LoadPlayers
/// deserialises the host's list on every peer, so whatever is put here is what every peer displays.
/// </param>
public record PlayerFactionAssignment(int PeerId, List<Faction> Factions, string DisplayName = null);
