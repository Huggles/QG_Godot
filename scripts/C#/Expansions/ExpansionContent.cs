using Godot;
using System.Collections.Generic;
using System.Reflection;
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
    /// <summary>
    /// Faction rows, in the shape of QGData_Factions_V2.json. A row whose Index is a new faction adds
    /// it (France, China); a row whose Index already exists patches that faction with the fields it
    /// names and nothing else (the base factions' NumberOfAirUnits).
    /// </summary>
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

    /// <summary>
    /// Applies the enabled expansions' faction rows to <paramref name="factions"/>: new factions are
    /// appended, existing ones patched field by field. See <see cref="FactionsFile"/>.
    /// </summary>
    public static void AddFactions(List<FactionData> factions)
    {
        foreach ((string id, List<JsonElement> rows) in Read<List<JsonElement>>(FactionsFile))
        {
            foreach (JsonElement row in rows)
            {
                if (!row.TryGetProperty(nameof(FactionData.Index), out JsonElement index))
                    throw new System.Exception($"Expansion '{id}': a row in {FactionsFile} has no Index");

                FactionData existing = factions.Find(faction => faction.Index == index.GetInt32());
                if (existing == null)
                    factions.Add(row.Deserialize<FactionData>());
                else
                    Patch(id, existing, row);
            }
        }
    }

    /// <summary>
    /// Overwrite the fields <paramref name="row"/> names on <paramref name="faction"/>, leaving the rest
    /// as the base data has them. Index identifies the row and is never written.
    /// </summary>
    private static void Patch(string expansionId, FactionData faction, JsonElement row)
    {
        foreach (JsonProperty field in row.EnumerateObject())
        {
            if (field.Name == nameof(FactionData.Index)) continue;

            PropertyInfo property = typeof(FactionData).GetProperty(field.Name);
            if (property == null || !property.CanWrite)
                throw new System.Exception($"Expansion '{expansionId}' patches {faction.Faction}.{field.Name}, which is not a faction field");

            property.SetValue(faction, field.Value.Deserialize(property.PropertyType));
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
