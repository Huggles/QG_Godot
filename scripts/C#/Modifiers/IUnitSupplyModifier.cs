/// <summary>
/// Implemented by status cards (or other sources) that can override the supply status
/// of a specific unit. When any active modifier returns true for a unit, that unit is
/// considered in supply regardless of the pathfinding result.
/// </summary>
public interface IUnitSupplyModifier : IModifier
{
    /// <summary>
    /// Returns true if this modifier grants supply to the given unit, bypassing normal
    /// pathfinding. Return false to express no opinion.
    /// </summary>
    bool GrantsSupply(UnitState unit);
}
