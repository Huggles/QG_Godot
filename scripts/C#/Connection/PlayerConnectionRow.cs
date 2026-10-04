/// <summary>How a player's connection looks from the host.</summary>
public enum SeatState
{
    Connected,
    /// <summary>No answer for a few seconds; nothing is held yet.</summary>
    Unstable,
    /// <summary>Unreachable: the game is paused for them.</summary>
    Absent,
    /// <summary>A bot is playing their factions.</summary>
    Bot,
    /// <summary>Back and being caught up; the game stays paused until they are.</summary>
    Rejoining
}

/// <summary>One line of the host's player table, as every peer receives it.</summary>
/// <param name="SecondsLeft">For an absent player, seconds until the host is asked what to do; 0 after.</param>
public sealed record PlayerConnectionRow(int SeatId, string Name, int PingMs, SeatState State, int SecondsLeft, bool IsHost);
