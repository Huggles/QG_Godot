using System.Collections.Generic;
using System.Text.Json.Serialization;

public class InitialGameStateData
{
    [JsonPropertyName("factions")]
    public Dictionary<string, FactionScenarioData> Factions { get; set; } = new Dictionary<string, FactionScenarioData>();

    [JsonPropertyName("startingFaction")]
    public string StartingFaction { get; set; } = null;

    [JsonPropertyName("startingRound")]
    public int StartingRound { get; set; } = 1;

    [JsonPropertyName("maxRounds")]
    public int MaxRounds { get; set; } = 20;

    /// <summary>
    /// When true (the default), every faction opens on StaticGameData.OpeningHandSize and must
    /// immediately discard StaticGameData.OpeningDiscardCount, landing on the normal hand size.
    /// When false the opening hand is dealt straight at StaticGameData.HandSize and play begins.
    ///
    /// The scenario's answer is the default; the host can override it in the lobby, which is why
    /// GameFlow reads GameManager.PendingOpeningDiscard first.
    /// </summary>
    [JsonPropertyName("openingDiscard")]
    public bool OpeningDiscard { get; set; } = true;

    // When true, every playable faction is given a random starting VP in
    // [RandomStartingVPMin, RandomStartingVPMax], overriding per-faction startingVP.
    [JsonPropertyName("randomizeStartingVP")]
    public bool RandomizeStartingVP { get; set; } = false;

    [JsonPropertyName("randomStartingVPMin")]
    public int RandomStartingVPMin { get; set; } = 0;

    [JsonPropertyName("randomStartingVPMax")]
    public int RandomStartingVPMax { get; set; } = 20;

    /// <summary>
    /// Tutorial script to run alongside this scenario, or null for a normal game. Host-only, like
    /// MaxRounds and OpeningDiscard: the program it installs runs entirely server-side, and a tutorial
    /// is single player, so there is no peer for it to reach.
    /// </summary>
    [JsonPropertyName("tutorial")]
    public string TutorialScriptPath { get; set; } = null;

    // Step mutators active for this scenario. Empty by default, so a scenario that declares none
    // has none — the turn flow behaves exactly as it did before mutators existed.
    [JsonPropertyName("mutators")]
    public List<MutatorScenarioData> Mutators { get; set; } = new List<MutatorScenarioData>();
}

/// <summary>
/// One scenario-declared mutator. Name is a C# class extending StepMutator, resolved by reflection
/// the same way CardData.ExecutionClass resolves card logic.
/// </summary>
public class MutatorScenarioData
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    // Empty means every faction's turn.
    [JsonPropertyName("factions")]
    public List<string> Factions { get; set; } = new List<string>();

    [JsonPropertyName("fromRound")]
    public int FromRound { get; set; } = 1;

    [JsonPropertyName("toRound")]
    public int ToRound { get; set; } = int.MaxValue;

    // Lower runs first within a (step, timing) window; ties fall back to registration order.
    [JsonPropertyName("order")]
    public int Order { get; set; } = 0;
}

public class FactionScenarioData
{
    [JsonPropertyName("unitDeployments")]
    public List<UnitDeploymentData> UnitDeployments { get; set; } = new List<UnitDeploymentData>();

    [JsonPropertyName("initialCards")]
    public List<InitialCardEntry> InitialCards { get; set; } = new List<InitialCardEntry>();

    [JsonPropertyName("initialHandCards")]
    public List<InitialHandCardEntry> InitialHandCards { get; set; } = new List<InitialHandCardEntry>();

    /// <summary>
    /// Cards that start in this faction's discard pile, by UniqueName. Taken out of the draw deck
    /// during setup, so they are gone from the opening deal as well as visibly played — which is the
    /// point: it is how a scenario sets up a mid-game position for cards that read the discard pile
    /// (StatusGuards, EventFlexibleResources, ResponseRationing).
    /// </summary>
    [JsonPropertyName("initialDiscardedCards")]
    public List<InitialDiscardedCardEntry> InitialDiscardedCards { get; set; } = new List<InitialDiscardedCardEntry>();

    [JsonPropertyName("startingVP")]
    public int StartingVictoryPoints { get; set; } = 0;
}

public class InitialCardEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
}

public class InitialHandCardEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
}

public class InitialDiscardedCardEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
}

public class UnitDeploymentData
{
    [JsonPropertyName("countryName")]
    public string CountryName { get; set; }
}
