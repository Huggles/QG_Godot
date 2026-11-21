using System.Collections.Generic;

/// <summary>
/// Between deploy targets the projection rates equal, prefer the one the faction's goals name (a star brought
/// into reach, one to take or clear). Kept far below 1 VP so it never overturns a real difference.
/// </summary>
public sealed class PreferAgendaTargetRule : IBotRule
{
    private const double Scale = 0.001;

    public string Name => "prefer_agenda_target";

    public string Description => "Break ties between deploy targets toward the faction's goals";

    public double DefaultWeight => 1.0;

    public bool EnabledByDefault => true;

    public IReadOnlySet<string> Kinds { get; } = new HashSet<string> { "SelectCountry" };

    public IReadOnlySet<CliOptionKind> OptionKinds { get; } = new HashSet<CliOptionKind> { CliOptionKind.Country };

    public bool AppliesTo(BotDecision decision)
        => decision.Agenda != null && decision.DeployTargets().Count > 0
           && BotSides.Includes("bot_tiebreak_nudges", "none", decision.Faction);

    public void Apply(BotDecision decision, IBotVerdictSink sink)
    {
        for (int i = 0; i < decision.Options.Count; i++)
        {
            int countryId = decision.Options[i].Id;
            if (decision.Options[i].Kind != CliOptionKind.Country) continue;
            if (!PreferVacantDeployRule.DeployingFaction(decision, countryId, out Faction deployer) || deployer != decision.Faction) continue;

            double goal = decision.Agenda.ValueFor(countryId);
            if (goal > 0) sink.Score(i, goal * Scale);
        }
    }
}
