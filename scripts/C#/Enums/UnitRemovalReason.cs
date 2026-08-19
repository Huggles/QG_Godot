using Godot;
using System;

/// <summary>
/// RECALL is not a game event: it is the piece a faction takes off the board to free a unit for a
/// deploy its pool cannot pay for (see UnitPoolShortfall). Its RemoveUnitChangeEvent carries both
/// IsTrigger = false and RegisterInPool = false, so nothing can react to it or read it back — the
/// reason exists so the log and the history read honestly, not to be matched on.
/// </summary>
public enum UnitRemovalReason
{
    BATTLE, ELIMINATE, SUPPLY, RECALL
}
