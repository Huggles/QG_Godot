using System.Collections.Generic;

/// <summary>
/// When choosing which card to play or activate, prefer the one whose projected outcome is worth the
/// most victory points — see <see cref="ProjectionValuer.ValueCard"/>, or with <c>bot_lookahead</c> on,
/// the best line of the faction's next own turns that opens with it (<see cref="TurnLookahead"/>).
///
/// A card the projection cannot value (a free-form step, a nested card play) is left unscored, i.e. at
/// 0: it neither wins nor loses on a number nobody computed. A card valued below 0 therefore ranks
/// under it, which is the point — that card is known to cost more than it gains on the board.
/// </summary>
public sealed class PreferValuableCardRule : IBotRule
{
    public string Name => "prefer_valuable_card";

    public string Description =>
        "When playing a card, prefer the one whose projected board outcome gains the most victory points";

    public double DefaultWeight => 1.0;

    public bool EnabledByDefault => true;

    public IReadOnlySet<string> Kinds { get; } = new HashSet<string> { "HandCardPlay", "ActivateCard" };

    public IReadOnlySet<CliOptionKind> OptionKinds { get; } = new HashSet<CliOptionKind> { CliOptionKind.Card };

    public bool AppliesTo(BotDecision decision) => true;

    public void Apply(BotDecision decision, IBotVerdictSink sink)
    {
        bool line = TurnLookahead.Applies(decision.Faction);
        if (line) EnemyTurnValue.NewDecision(decision.Faction);
        for (int i = 0; i < decision.Options.Count; i++)
        {
            if (decision.Options[i].Kind != CliOptionKind.Card) continue;

            int cardId = decision.Options[i].Id;
            double? value = line ? TurnLookahead.ValueLine(cardId, decision.Faction) : ProjectionValuer.ValueCard(cardId, decision.Faction);
            if (value is double v && v != 0) sink.Score(i, v);
        }
    }
}
