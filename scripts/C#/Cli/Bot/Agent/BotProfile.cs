using System.Collections.Generic;

/// <summary>
/// How much a given seat cares about each kind of goal, plus any goals only that faction has.
///
/// This is the type <see cref="AiSeatRuntime"/> has had a comment promising since the seat plumbing was
/// written — the place per-seat configuration was always going to live.
///
/// **Every profile currently returns the generic one.** That is deliberate, not unfinished: a weight is
/// only meaningful once something consumes the agenda, and nothing does yet. Hand-authoring six sets of
/// numbers before a single game has been measured with them would be inventing six untested opinions and
/// then having to defend them. The asymmetry the profiles will eventually key off is already in the data
/// and is not going anywhere — unit pools and home spaces differ sharply (Germany 7 armies / 3 navies,
/// the Soviets 7/1 out of Moscow, the US 5/6, Italy 4/3), and FactionData.excludeCards means Germany and
/// the US hold no Response cards, Japan no Events, the Soviets no Economic Warfare.
///
/// Weights multiply a goal's VP value, so 1.0 means "worth exactly what the scoreboard says". A profile
/// that wants a faction to fight harder for sea stars raises ClearEnemyStar rather than inventing a
/// separate sea-specific score, and the result stays comparable with every other goal on the agenda.
/// </summary>
public sealed class BotProfile
{
    /// <summary>Name as it appears in CLI args and in the trace.</summary>
    public string Name { get; init; } = "generic";

    /// <summary>
    /// Per-kind multiplier on a goal's VP value. A kind absent from the map weighs
    /// <see cref="DefaultWeight"/>, so a profile only writes down what it actually disagrees with.
    /// </summary>
    public IReadOnlyDictionary<GoalKind, double> GoalWeights { get; init; }
        = new Dictionary<GoalKind, double>();

    /// <summary>
    /// Goals this faction has that the generic set does not model. Empty for now; appended after the
    /// standard proposers so the ordering of the shared ones never depends on a profile.
    /// </summary>
    public IReadOnlyList<IGoalProposer> ExtraProposers { get; init; }
        = new List<IGoalProposer>();

    public const double DefaultWeight = 1.0;

    public double WeightFor(GoalKind kind)
        => GoalWeights != null && GoalWeights.TryGetValue(kind, out double w) ? w : DefaultWeight;

    /// <summary>Values every goal at exactly its victory-point worth. The baseline all six factions use.</summary>
    public static BotProfile Generic() => new() { Name = "generic" };

    /// <summary>
    /// The profile for a seat. Generic for every faction today — see the note on this class for why the
    /// weights are not yet written. The indirection exists so that when they are, no call site changes.
    /// </summary>
    public static BotProfile ForFaction(Faction faction) => new()
    {
        Name = faction == Faction.NONE ? "generic" : faction.ToString().ToLowerInvariant(),
    };
}
