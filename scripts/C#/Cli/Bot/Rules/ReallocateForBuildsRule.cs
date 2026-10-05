using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Reallocate Resources only to build. Four hand cards to knock one enemy unit out of a space is a bad
/// trade: the valuer prices the removal as if it lasts all game, but the enemy just rebuilds next turn.
/// So the take-a-card prompt vetoes Battle cards, and the play itself is vetoed when no Build card can be
/// taken, or scored down by however much a battle candidate was inflating it.
/// </summary>
public sealed class ReallocateForBuildsRule : IBotRule
{
    public string Name => "reallocate_for_builds";

    public string Description => "Use Reallocate Resources only to take a Build card, never a Battle card";

    public double DefaultWeight => 1.0;

    public bool EnabledByDefault => false;

    public IReadOnlySet<string> Kinds { get; } = new HashSet<string> { "HandCardPlay", "ActivateCard", "SelectCard" };

    public IReadOnlySet<CliOptionKind> OptionKinds { get; } = new HashSet<CliOptionKind> { CliOptionKind.Card };

    public bool AppliesTo(BotDecision decision) => true;

    public void Apply(BotDecision decision, IBotVerdictSink sink)
    {
        if (decision.Spec.Kind == "SelectCard") VetoBattleTakes(decision, sink);
        else JudgeActivation(decision, sink);
    }

    /// <summary>The prompt Reallocate's play step raises: keep the Build cards, drop the Battle cards.</summary>
    private static void VetoBattleTakes(BotDecision decision, IBotVerdictSink sink)
    {
        if (!GameSession.Current.GameState.CardStepsById.TryGetValue(decision.Request.OriginStepId, out CardStep step)) return;
        if (step.Kind != StepKind.PlayCard || step.CardLogic is not MutatorReallocateResources) return;

        for (int i = 0; i < decision.Options.Count; i++)
            if (IsBattle(decision.Options[i].Id)) sink.Veto(i, "Reallocate Resources for a battle");
    }

    /// <summary>Reallocate in the play prompt: worth only its best Build take, and nothing without one.</summary>
    private static void JudgeActivation(BotDecision decision, IBotVerdictSink sink)
    {
        for (int i = 0; i < decision.Options.Count; i++)
        {
            if (CardState.ForId(decision.Options[i].Id)?.CardLogic is not MutatorReallocateResources reallocate) continue;
            List<int> candidates = Candidates(reallocate);
            if (candidates == null) continue;

            List<int> builds = candidates.Where(id => !IsBattle(id)).ToList();
            if (builds.Count == 0) { sink.Veto(i, "Reallocate Resources has no Build card to take"); continue; }
            if (builds.Count == candidates.Count) continue;

            double best = BestPlay(candidates, decision.Faction), bestBuild = BestPlay(builds, decision.Faction);
            if (double.IsFinite(best) && double.IsFinite(bestBuild) && bestBuild < best) sink.Score(i, bestBuild - best);
        }
    }

    private static List<int> Candidates(MutatorReallocateResources reallocate)
    {
        CardStep play = reallocate.CardSteps.FirstOrDefault(s => s.Kind == StepKind.PlayCard);
        try { return play?.PossiblePlays(StepContext.Live(null))?.ToList(); }
        catch (System.Exception) { return null; }
    }

    private static double BestPlay(List<int> cardIds, Faction faction)
        => cardIds.Select(id => ProjectionValuer.ValuePlay(id, faction) ?? double.NegativeInfinity).DefaultIfEmpty(0).Max();

    private static bool IsBattle(int cardId)
        => CardState.ForId(cardId)?.CardData is CardData data && BotBattleFacts.IsBattleCard(data.CardType);
}
