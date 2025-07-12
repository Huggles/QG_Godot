using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class VPTurnSummary : StateObject
{
    public int TurnNumber { get; private set; } = -1;
    public List<VPEntry> victoryPointEntries = new();
    public Faction Faction => (Faction)((TurnNumber-1) % StaticGameData.PlayableFactions.Count);

    public int TotalScore => victoryPointEntries.Map(vpe => vpe.VictoryPoints).Sum();

    public VPTurnSummary(int turnNumber)
    {
        TurnNumber = turnNumber;        
    }

    public void AddScore(VPEntry victoryPointEntry)
    {
        victoryPointEntries.Add(victoryPointEntry);
    }
}
