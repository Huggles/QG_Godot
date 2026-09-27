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
/// <param name="IsAi">
/// Whether this seat is played by a bot rather than by a person on some peer.
///
/// Carried on the assignment because that is the one description of the seating every peer
/// deserialises identically (NetworkApi.LoadPlayers), so every peer agrees on which seats are bots.
/// It used to be inferred from the peer id being at or above PlayerFactionRegistry.AiSeatIdBase —
/// which a real Godot client id, randomly drawn from the whole positive range, also is.
/// </param>
public record PlayerFactionAssignment(int PeerId, List<Faction> Factions, string DisplayName = null,
                                      bool IsAi = false);
