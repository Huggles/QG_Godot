using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class VPTurnSummary
{
    public int TurnNumber { get; private set; } = -1;
    public List<VPEntry> victoryPointEntries { get; set; }
    public Faction Faction => StaticGameData.PlayableFactions[(TurnNumber-1) % StaticGameData.PlayableFactions.Count];
    public int TotalScore => victoryPointEntries.Map(vpe => vpe.VictoryPoints).Sum();

    public VPTurnSummary(int turnNumber)
    {
        victoryPointEntries = new List<VPEntry>();
        TurnNumber = turnNumber;        
    }

    public void AddScore(VPEntry victoryPointEntry)
    {
        victoryPointEntries.Add(victoryPointEntry);
    }
}
