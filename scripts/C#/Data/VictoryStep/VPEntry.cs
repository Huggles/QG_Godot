using Godot;
using System;

public partial class VPEntry
{
    public int VictoryPoints { get; set; }
    public string Reason { get; set; }

    public VPEntry(int victoryPoints, string reason)
    {
        this.VictoryPoints = victoryPoints;
        this.Reason = reason;
    }
}
