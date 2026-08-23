using System.Collections.Generic;
using System.Linq;

/// <summary>
/// What an action could affect if it were used right now — the answer an
/// <see cref="ITargetSetProvider"/> gives.
///
/// Held as <see cref="TargetRef"/> entries rather than one list per kind so that adding a target kind
/// costs an enum value and nothing else: no new field, no new factory on every caller, no change to
/// the consumers. The same reasoning as <c>CliOption</c> in <c>InputRequestSpec.cs</c>.
///
/// Presentation only. Nothing in the execution path reads a TargetSet — the relationship it has to
/// the real selection a step performs is the one <see cref="CardStep.MeetAllAdvisoryConditions"/> has
/// to <see cref="CardStep.MeetAllConditions"/>. Declaring nothing is always valid and always safe.
///
/// Immutable: the entries are copied and deduplicated on construction, so a caller cannot hand out
/// its own live list and then mutate what a consumer is reading.
/// </summary>
public sealed record TargetSet
{
    public IReadOnlyList<TargetRef> Targets { get; }

    private TargetSet(IEnumerable<TargetRef> targets)
    {
        // Distinct rather than trusting the caller: a card that unions two overlapping sources (a
        // battle target list and an adjacency list) would otherwise report the same country twice,
        // and every consumer would have to dedupe for itself.
        Targets = targets?.Distinct().ToList() ?? new List<TargetRef>();
    }

    /// <summary>
    /// Declares nothing. A property rather than a static field so no caller can reach a shared
    /// instance — the list inside is immutable by contract, not by type.
    /// </summary>
    // An explicit empty list, not new(null): a record also gets a generated copy constructor, and a
    // bare null is ambiguous between that and the one below.
    public static TargetSet None => new(new List<TargetRef>());

    public bool IsEmpty => Targets.Count == 0;

    public static TargetSet Of(IEnumerable<TargetRef> targets) => new(targets);

    public static TargetSet Countries(IEnumerable<int> countryIds) =>
        Kind(TargetKind.Country, countryIds);

    /// <summary>For the target lists written as <c>Country</c> enum values rather than ids.</summary>
    public static TargetSet Countries(IEnumerable<Country> countries) =>
        Kind(TargetKind.Country, countries?.Select(country => (int)country));

    /// <summary>For the target lists held as <see cref="CountryState"/>.</summary>
    public static TargetSet Countries(IEnumerable<CountryState> countries) =>
        Kind(TargetKind.Country, countries?.Where(cs => cs != null).Select(cs => cs.Id));

    public static TargetSet Units(IEnumerable<int> unitIds) => Kind(TargetKind.Unit, unitIds);

    public static TargetSet Units(IEnumerable<UnitState> units) =>
        Kind(TargetKind.Unit, units?.Where(us => us != null).Select(us => us.Id));

    public static TargetSet Cards(IEnumerable<int> cardIds) => Kind(TargetKind.Card, cardIds);

    public static TargetSet Factions(IEnumerable<Faction> factions) =>
        Kind(TargetKind.Faction, factions?.Select(faction => (int)faction));

    /// <summary>
    /// For a selection that is "an enemy unit OR an empty country" — see <c>LandBattle</c> /
    /// <c>SeaBattle</c> and the eleven cards holding a <see cref="BattleTarget"/> list.
    /// <see cref="TargetType.NONE"/> entries are dropped rather than guessed at.
    /// </summary>
    public static TargetSet FromBattleTargets(IEnumerable<BattleTarget> battleTargets) =>
        new(battleTargets?
            .Where(target => target != null && target.Type != TargetType.NONE)
            .Select(target => new TargetRef(
                target.Type == TargetType.UNIT ? TargetKind.Unit : TargetKind.Country,
                target.Id)));

    /// <summary>
    /// The union of this set and another, deduplicated. For an action whose targets come from more
    /// than one source — a battle card offering both attackable countries and attackable units.
    /// </summary>
    public TargetSet Plus(TargetSet other) =>
        other == null ? this : new(Targets.Concat(other.Targets));

    public IEnumerable<int> IdsOf(TargetKind kind) =>
        Targets.Where(target => target.Kind == kind).Select(target => target.Id);

    public IEnumerable<int> CountryIds => IdsOf(TargetKind.Country);
    public IEnumerable<int> UnitIds => IdsOf(TargetKind.Unit);
    public IEnumerable<int> CardIds => IdsOf(TargetKind.Card);

    private static TargetSet Kind(TargetKind kind, IEnumerable<int> ids) =>
        new(ids?.Select(id => new TargetRef(kind, id)));
}
