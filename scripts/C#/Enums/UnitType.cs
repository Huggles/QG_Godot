using Godot;
using System;

public enum UnitType
{
    ARMY,
    NAVY,
    ANY,

    /// <summary>
    /// Shares a space with its own faction's Army or Navy and defends it; never occupies, supplies,
    /// battles or scores on its own. Only an expansion that gives factions air units puts any on the
    /// board. After ANY so the existing values keep their numbers.
    /// </summary>
    AIR_FORCE
}

public static class UnitTypeExtensions
{
    /// <summary>Returns true if this type is the ANY wildcard.</summary>
    public static bool ANY(this UnitType filter) => filter == UnitType.ANY;

    /// <summary>
    /// Returns true if this filter is ANY and <paramref name="target"/> is an Army or a Navy, or the
    /// filter equals <paramref name="target"/>. ANY deliberately leaves Air Forces out: every card
    /// written with it means "your unit" in the base game's sense, and an Air Force is not one of
    /// those — a card that means Air Forces names AIR_FORCE.
    /// </summary>
    public static bool Matches(this UnitType filter, UnitType target)
        => filter.ANY() ? target != UnitType.AIR_FORCE : filter == target;
}
