using System.Collections.Generic;

/// <summary>With an empty unit pool, give up the unit whose loss costs the faction least (UnitPoolShortfall's prompt).</summary>
public sealed class PreferCheapRecallRule : IBotRule
{
    public string Name => "prefer_cheap_recall";

    public string Description =>
        "When the pool is empty and a unit must be removed to build, remove the one whose loss costs the least";

    public double DefaultWeight => 1.0;

    public bool EnabledByDefault => true;

    public IReadOnlySet<string> Kinds { get; } = new HashSet<string> { "SelectUnit" };

    public IReadOnlySet<CliOptionKind> OptionKinds { get; } = new HashSet<CliOptionKind> { CliOptionKind.Unit };

    public bool AppliesTo(BotDecision decision)
        => decision.Request is InputRequest.SelectUnitRequestHandler { IsRecall: true }
           && BotSides.Includes("bot_fund_builds", "none", decision.Faction);

    public void Apply(BotDecision decision, IBotVerdictSink sink)
    {
        for (int i = 0; i < decision.Options.Count; i++)
        {
            CliOption option = decision.Options[i];
            if (option.Kind != CliOptionKind.Unit) continue;

            RemoveUnitChangeEvent recall = new(decision.Faction, option.Id, UnitRemovalReason.RECALL) { IsTrigger = false, RegisterInPool = false };
            double? value = ProjectionValuer.ValueOf(recall, decision.Faction);
            if (value == null) sink.Unvalued(i);
            else if (value.Value != 0) sink.Score(i, value.Value);
        }
    }
}
