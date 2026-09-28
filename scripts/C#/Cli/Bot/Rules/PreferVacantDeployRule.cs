using System.Collections.Generic;

/// <summary>
/// When choosing where to place a unit, prefer a country this faction does not already occupy.
///
/// Deploying onto a country you already hold rebuilds the piece standing there and leaves the board
/// identical — CountryState.CanBuild permits it (a faction's own unit is not adjacent to itself, so
/// "rebuild in place" is a legal target rather than a loophole), GameAPI rebuilds, and nothing changes.
/// It is the motivating case for the whole advisory-condition mechanism: BuildArmy declares
/// HasVacantBuildableLand as an advisory precisely so the card can be drawn with a caution scrim when
/// this is all it can do.
///
/// This rule reuses <see cref="Condition.CountryHasFactionUnit"/> — the same predicate the board-level
/// advisory HasVacantBuildableLand is built from — rather than reimplementing "vacant". A one-element
/// id list turns that Condition into a per-option test, and using it means the bot and the card can
/// never disagree about which countries count as vacant.
///
/// A PREFERENCE, not a veto, for two independent reasons:
///  - Rebuilding in place is sometimes the only legal answer, and it still raises a reactable
///    DeployUnitChangeEvent — a real code path the sim also exists to fuzz. Vetoing it everywhere
///    would stop that path being walked at all.
///  - The engine's floor would have to rescue every prompt where all targets are occupied, which
///    turns a clear preference into a veto-then-rescue round trip that shows up as `floored` noise
///    and tells you nothing.
///
/// Gated on the asking step's listed outcomes (<see cref="BotDecision.DeployTargets"/>): it fires only
/// on options that would deploy. Without that gate this rule is actively harmful rather than merely
/// useless: "prefer a country I do not occupy" applied to a prompt asking "which of your own units'
/// spaces?" prefers the space with no unit in it.
/// </summary>
public sealed class PreferVacantDeployRule : IBotRule
{
    private const double VacantBonus = 2;

    public string Name => "prefer_vacant_deploy";

    public string Description =>
        "When deploying, prefer a country this faction does not already occupy over rebuilding in place";

    public double DefaultWeight => 1.0;

    public bool EnabledByDefault => true;

    public IReadOnlySet<string> Kinds { get; } = new HashSet<string> { "SelectCountry" };

    public IReadOnlySet<CliOptionKind> OptionKinds { get; } = new HashSet<CliOptionKind>
    {
        CliOptionKind.Country,
    };

    /// <summary>
    /// The declared purpose is the whole gate. See the class note: an undeclared prompt must be left
    /// alone, because this rule's advice is inverted on some of the prompts it would otherwise match.
    /// </summary>
    public bool AppliesTo(BotDecision decision)
        => decision.DeployTargets().Count > 0;

    /// <summary>
    /// Who deploys if this option is chosen, read off the step's outcomes — so Arsenal of Democracy
    /// scores for the UK, which builds. False for an option that does not deploy.
    /// </summary>
    internal static bool DeployingFaction(BotDecision decision, int countryId, out Faction deployer)
    {
        return decision.DeployTargets().TryGetValue(countryId, out deployer);
    }

    public void Apply(BotDecision decision, IBotVerdictSink sink)
    {
        for (int i = 0; i < decision.Options.Count; i++)
        {
            if (decision.Options[i].Kind != CliOptionKind.Country) continue;
            if (!DeployingFaction(decision, decision.Options[i].Id, out Faction deployer)) continue;

            Condition occupied = new Condition.CountryHasFactionUnit(decision.Options[i].Id, deployer);
            if (!occupied.MeetCondition()) sink.Score(i, VacantBonus);
        }
    }
}
