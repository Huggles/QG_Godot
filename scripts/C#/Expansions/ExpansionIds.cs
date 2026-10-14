using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// The one shape a list of expansion ids takes everywhere it travels: distinct and in ordinal order, so
/// the same choice reads identically on every peer, in a save and on the wire, whatever order a menu or
/// a command line happened to list it in. Expansions apply their content in this order.
/// </summary>
public static class ExpansionIds
{
    public static List<string> Normalize(IEnumerable<string> ids) => ids
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .Select(id => id.Trim())
        .Distinct(StringComparer.Ordinal)
        .OrderBy(id => id, StringComparer.Ordinal)
        .ToList();

    /// <summary>For an RPC argument: comma-separated, empty for none.</summary>
    public static string ToWire(IEnumerable<string> ids) => string.Join(",", Normalize(ids));

    public static List<string> FromWire(string wire)
        => Normalize((wire ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// The <c>expansions=</c> command-line argument: a comma-separated list, or <c>none</c> to play the
    /// base game even when the scenario asks for expansions.
    /// </summary>
    public static List<string> FromCliArg(string value)
        => string.Equals(value, "none", StringComparison.OrdinalIgnoreCase) ? new List<string>() : FromWire(value);

    /// <summary>A scenario file's optional <c>"expansions": ["total_war"]</c>. Absent means none.</summary>
    public static List<string> FromScenario(JsonElement root)
    {
        if (!root.TryGetProperty("expansions", out JsonElement element) || element.ValueKind != JsonValueKind.Array)
            return new List<string>();

        return Normalize(element.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()));
    }

    /// <summary>Player-facing, e.g. "Total War", or "none".</summary>
    public static string Describe(IEnumerable<string> ids)
    {
        List<string> names = Normalize(ids).Select(ExpansionRegistry.DisplayName).ToList();
        return names.Count == 0 ? "none" : string.Join(", ", names);
    }
}
