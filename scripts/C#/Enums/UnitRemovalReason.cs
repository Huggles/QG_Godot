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
    BATTLE, ELIMINATE, SUPPLY, RECALL,

    // Air Force removals, appended so the values above keep their numbers in saves and on the wire.
    // None is a battle (BattleCountryChangeEvent.IsBattle), and like RECALL they are applied outside
    // the reaction pipeline. See AirForces.

    /// <summary>The defender gave up this Air Force to save the Army or Navy being battled in its space.</summary>
    AIR_DEFENSE,
    /// <summary>The attacker gave up this Air Force to remove that Army or Navy anyway.</summary>
    AIR_ATTACK,
    /// <summary>Eliminated by an enemy gaining air superiority.</summary>
    AIR_SUPERIORITY,
    /// <summary>Left without its own Army or Navy, with no adjacent space to reposition to.</summary>
    REPOSITION_LOST
}
