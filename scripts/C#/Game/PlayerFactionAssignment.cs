using System.Collections.Generic;

/// <summary>Maps a network peer ID to the factions they control in the game.</summary>
public record PlayerFactionAssignment(int PeerId, List<Faction> Factions);
