using System.Collections.Generic;
using System.Linq;

/// <summary>
/// When a card step asks for its target, prefer the option whose outcome is worth the most victory
/// points. The asking step lists what every option would do (<see cref="CardStep.PossibleOutcomes()"/>),
/// so each option is valued by projecting exactly the event it causes.
///
/// Fires only on the step's OWN choice: a prompt raised under the step for something else — the
/// unit recall an empty pool forces, a reaction window — matches no listed option and is left alone.
/// </summary>
public sealed class PreferValuableTargetRule : IBotRule
{
    public string Name => "prefer_valuable_target";

    public string Description =>
        "When a card asks for a target, prefer the one whose projected outcome gains the most victory points";

    public double DefaultWeight => 1.0;

    public bool EnabledByDefault => false;

    public IReadOnlySet<string> Kinds { get; } = new HashSet<string>
    {
        "SelectCountry", "SelectUnit", "SelectBattleTarget", "SelectFaction",
    };

    public IReadOnlySet<CliOptionKind> OptionKinds { get; } = new HashSet<CliOptionKind>
    {
        CliOptionKind.Country, CliOptionKind.Unit, CliOptionKind.Faction,
    };

    public bool AppliesTo(BotDecision decision) => decision.Request.OriginStepId >= 0;

    public void Apply(BotDecision decision, IBotVerdictSink sink)
    {
        if (!GameSession.Current.GameState.CardStepsById.TryGetValue(decision.Request.OriginStepId, out CardStep step)) return;

        IReadOnlyList<StepOption> outcomes = step.PossibleOutcomes();
        if (outcomes == null) return;
        try
        {
            Dictionary<TargetRef, ChangeEvent> byTarget = outcomes
                .Where(o => o.Target != null)
                .GroupBy(o => o.Target.Value)
                .ToDictionary(g => g.Key, g => g.First().Event);

            for (int i = 0; i < decision.Options.Count; i++)
            {
                CliOption option = decision.Options[i];
                TargetKind? kind = option.Kind switch
                {
                    CliOptionKind.Country => TargetKind.Country,
                    CliOptionKind.Unit => TargetKind.Unit,
                    CliOptionKind.Faction => TargetKind.Faction,
                    _ => null,
                };
                if (kind == null || !byTarget.TryGetValue(new TargetRef(kind.Value, option.Id), out ChangeEvent outcome)) continue;

                double? value = ProjectionValuer.ValueOf(outcome, decision.Faction);
                if (value is double v && v != 0) sink.Score(i, v);
            }
        }
        finally
        {
            StepChoice.Release(outcomes);
        }
    }
}
