using System.Collections.Generic;

/// <summary>
/// When choosing where to place a unit, prefer a supply-star country — the only thing on the board
/// that pays victory points every turn.
///
/// The weights are read straight off the scoring rule rather than chosen by taste.
/// VictoryStepHandlerDefault.ScoreSupplyCountryVPs pays each faction
/// <c>max(3 - occupantCount, 1)</c> per turn for every supply-star country it occupies, so:
///
///   empty star                → you become sole occupant, 3 VP/turn                          → +3
///   star held by an ALLY only → you score 2 and the ally drops from 3 to 2, team net +1       → +1
///   star you already occupy   → nothing changes                                              →  0
///   not a star                → no per-turn income                                           →  0
///
/// The middle case is worth stating because it is counter-intuitive and easy to get backwards: piling
/// onto a teammate's star is a small GAIN for the team, not a loss, but a much smaller one than taking
/// an empty star. A rule that treated all stars alike would spend deploys crowding its own team.
///
/// Deliberately says nothing about enemy-held stars. Deploying into one is not possible under
/// CanRecruit ("empty or occupied by my own team"), so any star still on offer is empty or friendly and
/// the two cases above are exhaustive.
///
/// Composes with <see cref="PreferVacantDeployRule"/> by addition: an empty star scores +3 there and
/// +2 here for being vacant, so it outranks a star the faction already sits on (0 + 0) and a vacant
/// non-star (0 + 2). That ordering is the intended policy and is why both are preferences rather than
/// vetoes — vetoes cannot express "better", only "forbidden".
/// </summary>
public sealed class PreferSupplyStarDeployRule : IBotRule
{
    private const double EmptyStarBonus = 3;
    private const double AlliedStarBonus = 1;

    public string Name => "prefer_supply_star_deploy";

    public string Description =>
        "When deploying, prefer a supply-star country — an empty one most, an ally-held one a little";

    public double DefaultWeight => 1.0;

    public bool EnabledByDefault => false;

    public IReadOnlySet<string> Kinds { get; } = new HashSet<string> { "SelectCountry" };

    public IReadOnlySet<CliOptionKind> OptionKinds { get; } = new HashSet<CliOptionKind>
    {
        CliOptionKind.Country,
    };

    /// <inheritdoc cref="PreferVacantDeployRule.AppliesTo"/>
    public bool AppliesTo(BotDecision decision)
        => decision.Spec.OriginPurpose == PromptPurpose.DEPLOY_TARGET;

    public void Apply(BotDecision decision, IBotVerdictSink sink)
    {
        for (int i = 0; i < decision.Options.Count; i++)
        {
            if (decision.Options[i].Kind != CliOptionKind.Country) continue;

            CountryState country = CountryState.ForId(decision.Options[i].Id);
            if (country == null || !country.IsSupply) continue;

            if (country.IsCountryEmpty) sink.Score(i, EmptyStarBonus);
            else if (!country.HasUnit(decision.Faction)) sink.Score(i, AlliedStarBonus);
            // else: a star this faction already holds. Redeploying there earns nothing.
        }
    }
}
