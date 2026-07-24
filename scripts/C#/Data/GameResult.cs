using System.Collections.Generic;

/// <summary>
/// Serializable end-of-game summary. Built on the host when a win condition is met,
/// broadcast to all peers via MultiplayerSession, and stashed in
/// VictoryScreen.PendingResult before the scene switches (the game state is destroyed
/// on scene change, so the screen reads only this object).
/// </summary>
public class GameResult
{
    public FactionTeam WinningTeam { get; set; }   // AXIS wins ties
    public bool IsDraw { get; set; }               // false with the current tie rule; kept for display flexibility
    public int AxisTotal { get; set; }
    public int AlliesTotal { get; set; }
    public int FinalRound { get; set; }
    public string EndReason { get; set; }          // "30-point lead" | "Round 20 reached"
    public List<FactionResult> Factions { get; set; } = new();
}

public class FactionResult
{
    public Faction Faction { get; set; }
    public FactionTeam Team { get; set; }
    public int Total { get; set; }
    public List<RoundScore> PerRound { get; set; } = new();  // one entry per round the faction scored
    public FactionData FactionData { get; set; }             // display data (label, colour, flag); may be null
}

public class RoundScore
{
    public int Round { get; set; }
    public int Points { get; set; }
}
