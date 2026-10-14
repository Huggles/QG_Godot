using Godot;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// The data files this session's expansions add to the base game's. Each expansion keeps them under
/// <c>res://expansions/&lt;id&gt;/data/</c>, named after what they extend; only the expansions enabled for
/// the session (<see cref="GameOptions.EnabledExpansions"/>) are read, so the base game reads nothing here.
///
/// Additions are appended after the base data, in GameOptions order, so everything the base game numbers
/// by load order (unit ids, card ids) keeps its number.
/// </summary>
public static class ExpansionContent
{
    /// <summary>Faction rows to add: the minor factions. Same shape as QGData_Factions_V2.json.</summary>
    public const string FactionsFile = "factions.json";

    /// <summary>
    /// Scenario additions: a scenario file's shape, of which only "factions" is read. A faction the
    /// scenario itself sets up keeps the scenario's setup; any other is added, e.g. France's starting
    /// Army in Western Europe.
    /// </summary>
    public const string SetupFile = "setup.json";

    /// <summary>
    /// <paramref name="fileName"/> from each enabled expansion that ships one, deserialized, with the id
    /// of the expansion it came from. Throws on a file that does not parse: an expansion with broken data
    /// must stop the session rather than start a game missing half of itself.
    /// </summary>
    public static List<(string ExpansionId, T Content)> Read<T>(string fileName)
    {
        List<(string, T)> contents = new();
        foreach (string id in GameOptions.EnabledExpansions)
        {
            string path = $"{ExpansionRegistry.Find(id).ResFolder}data/{fileName}";
            if (!FileAccess.FileExists(path)) continue;

            try
            {
                contents.Add((id, JsonSerializer.Deserialize<T>(FileAccess.GetFileAsString(path))));
            }
            catch (JsonException e)
            {
                throw new System.Exception($"Expansion '{id}': {path} is not valid: {e.Message}", e);
            }
        }
        return contents;
    }

    /// <summary>Adds the enabled expansions' faction rows to <paramref name="factions"/>.</summary>
    public static void AddFactions(List<FactionData> factions)
    {
        foreach ((string id, List<FactionData> rows) in Read<List<FactionData>>(FactionsFile))
        {
            foreach (FactionData row in rows)
            {
                // Replacing a base faction's row would renumber nothing but silently change its rules;
                // an expansion that wants that needs a deliberate merge, not a second row.
                if (factions.Exists(existing => existing.Index == row.Index))
                    throw new System.Exception($"Expansion '{id}' adds faction {row.Faction}, which already exists");
                factions.Add(row);
            }
        }
    }

    /// <summary>Adds the enabled expansions' faction setups to a scenario, leaving the scenario's own alone.</summary>
    public static void AddSetup(InitialGameStateData scenario)
    {
        foreach ((string id, InitialGameStateData setup) in Read<InitialGameStateData>(SetupFile))
        {
            foreach ((string factionKey, FactionScenarioData factionSetup) in setup.Factions)
            {
                if (scenario.Factions.TryAdd(factionKey, factionSetup))
                    DebugUtilities.PrintPeer($"Expansion '{id}' sets up {factionKey}");
            }
        }
    }
}
