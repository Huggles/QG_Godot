using Godot;
using System;

public partial class VPEntry : GodotObject
{
    public int VictoryPoints;
    public string Reason;

    public VPEntry(int victoryPoints, string reason)
    {
        this.VictoryPoints = victoryPoints;
        this.Reason = reason;
    }
}
