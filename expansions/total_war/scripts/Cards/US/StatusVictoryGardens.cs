using System;

/// <summary>Passive: no trigger and no steps. Reallocate Resources costs the US 1 card, whatever the session's base cost.</summary>
public partial class StatusVictoryGardens : StatusCardLogic, IReallocateCostModifier
{
    public int ReallocateCost(int cost) => Math.Min(cost, 1);
}
