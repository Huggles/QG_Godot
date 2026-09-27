using System.Collections.Generic;

/// <summary>
/// When choosing where to place a unit, prefer the country that adds the most victory points per turn —
/// which in practice means a supply star, the only thing on the board that pays income.
///
/// **The score IS the victory-point delta.** It is not a weight chosen to rank the cases in the right
/// order; it is <see cref="VpMath.VpDeltaOfDeploy"/>, the game's own scoring rule
/// (<see cref="VictoryStepHandlerDefault.ScoreSupplyCountryVPs"/>) applied to the board that would exist
/// after this deploy. Every case below falls out of that call rather than being enumerated here:
///
/// <code>
///   empty star                      you become sole occupant, +2/turn           -> +2
///   star held by ONE ally           you gain 1, the ally drops 2 -> 1, net 0    ->  0
///   star held by TWO allies         3 occupants pay 1 each, was 2, net +1       -> +1
///   star this faction already holds rebuilding in place changes nothing         ->  0
///   not a star                      no per-turn income                          ->  0
/// </code>
///
/// This rule previously used hand-derived constants of +3 and +1, and both were wrong. It documented an
/// empty star as paying 3 VP/turn; the formula is <c>max(3 - occupantCount, 1)</c> and the scoring loop
/// walks <see cref="FactionState.OccupiedCountryIds"/>, which is built from the faction's own active
/// units — so a country being scored always counts that faction among its occupants and the count is
/// never zero. A solo star pays 2. It then reasoned that joining an ally was "team net +1" on the
/// strength of the ally falling from 3 to 2, when in fact the ally falls from 2 to 1 while the newcomer
/// gains 1, for a net of nothing.
///
/// Neither error was visible in play. The resulting ORDER was roughly right, so the bot looked like it
/// was working while it spent deploys on ally-held stars worth nothing to the team and passed over
/// vacant ground that at least extended its reach. That is the argument for asking the formula rather
/// than restating it: a hand-derived constant can be wrong in a way nothing ever reports, and the
/// two-allies case above is one no fixed pair of constants can express at all.
///
/// Composes with <see cref="PreferVacantDeployRule"/> by addition, which contributes +2 for a country
/// this faction does not already occupy:
///
/// <code>
///   empty star                4   (2 + 2)
///   star held by two allies   3   (1 + 2)
///   star held by one ally     2   (0 + 2)
///   vacant non-star           2   (0 + 2)
///   anywhere already occupied 0   (0 + 0)
/// </code>
///
/// The tie between a one-ally star and a vacant non-star is correct rather than a rough edge: both add
/// exactly nothing to the team's income this turn, so there is no victory-point reason to prefer either.
/// Breaking that tie is a question about board position and reach, which this rule has no view on.
/// </summary>
public sealed class PreferSupplyStarDeployRule : IBotRule
{
    public string Name => "prefer_supply_star_deploy";

    public string Description =>
        "When deploying, prefer the country that adds the most victory points per turn";

    public double DefaultWeight => 1.0;

    public bool EnabledByDefault => true;

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
        FactionTeam enemyTeam = StaticGameData.OpponentFactionTeamForFaction(decision.Faction);

        for (int i = 0; i < decision.Options.Count; i++)
        {
            if (decision.Options[i].Kind != CliOptionKind.Country) continue;

            CountryState country = CountryState.ForId(decision.Options[i].Id);
            if (country == null) continue;

            // Enemy-held countries are not deploy targets under CountryState.CanRecruit, so this should
            // never fire. It is here because VpDeltaOfDeploy answers the hypothetical it is asked without
            // checking whether the deploy is legal — that is the offer set's job — and taking an enemy
            // square off them would price as a gain. Guarding the valuation is cheaper than relying on
            // every future prompt to have filtered for us.
            if (country.OccupyingTeam == enemyTeam) continue;

            int vpPerTurn = VpMath.VpDeltaOfDeploy(country, decision.Faction);

            // Skipping zero rather than scoring it: a zero score changes no ranking but does count as a
            // firing in this rule's stats, and "scored 400 times, none of them meaningfully" is exactly
            // the sort of number that makes a rules_stats column lie.
            if (vpPerTurn <= 0) continue;

            sink.Score(i, vpPerTurn);
        }
    }
}
