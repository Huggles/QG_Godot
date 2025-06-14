using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class VPTurnSummary : StateObject
{
    public int TurnNumber { get; private set; } = -1;
    public Dictionary<string, int> ScoresForReason = new();

    public int TotalScore => ScoresForReason.Values.Sum();

    public VPTurnSummary(int turnNumber)
    {
        TurnNumber = turnNumber;
        ScoresForReason = new Dictionary<string, int>();
    }

    public void AddScore(int points, string reason)
    {
        ScoresForReason[reason] = points;
    }
}
