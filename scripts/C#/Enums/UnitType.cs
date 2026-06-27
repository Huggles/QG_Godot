using Godot;
using System;

public enum UnitType
{
    ARMY,
    NAVY,
    ANY
}

public static class UnitTypeExtensions
{
    /// <summary>Returns true if this type is the ANY wildcard.</summary>
    public static bool ANY(this UnitType filter) => filter == UnitType.ANY;
    /// <summary>Returns true if this filter is ANY, or equals <paramref name="target"/>.</summary>
    public static bool Matches(this UnitType filter, UnitType target) => filter.ANY() || filter == target;
}
