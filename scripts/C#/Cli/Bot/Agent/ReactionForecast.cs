using System.Collections.Generic;
using System.Linq;

/// <summary>
/// What an event does once the after-reaction window it opens has closed: Germany battling a land space
/// is worth what Blitzkrieg then builds there, and an enemy face-up card that punishes the move costs
/// what it takes.
///
/// The window is played out on a fork the way CardPlayRound.RequestAfterReactions plays it live: the
/// team that did not cause the event goes first, then the other. A card is a candidate when the valuing
/// faction can see it — every card of its own team, the enemy's Status cards and revealed Responses —
/// and its real triggers fire in the situation after the event, on the forked board. Each candidate's
/// steps are projected, and its controller plays it only when that raises their own team's value;
/// a reaction is always optional. A reaction's own events open their windows in turn, to
/// <see cref="MaxDepth"/>.
///
/// Before an event applies, its block window is forecast too — for a valuer under
/// <c>bot_enemy_turns</c>, and always while <see cref="EnemyTurnValue"/> measures: the team that did
/// not cause the event may block it with a card whose real triggers accept it (Radar saving a supplied
/// US Navy). The blocker pays the card's cost steps, the event never happens, and it is played only
/// when that is worth more to its team than letting the event through.
/// </summary>
public static class ReactionForecast
{
    /// <summary>How many windows deep a chain of reactions is followed.</summary>
    public const int MaxDepth = 2;

    /// <summary>
    /// Who forecasts: <c>bot_forecast=all|none|AXIS|ALLIES</c> on the command line, all by default. One
    /// side on and the other off is how the forecast is measured against its absence.
    /// </summary>
    private static bool Forecasts(Faction valuer) => BotSides.Includes("bot_forecast", "all", valuer);

    /// <summary>Who forecasts block windows; see the summary.</summary>
    private static bool ForecastsBlocks(Faction valuer) =>
        EnemyTurnValue.Measuring || BotSides.Includes("bot_enemy_turns", "none", valuer);

    /// <summary>
    /// Apply <paramref name="changeEvent"/> to <paramref name="situation"/>'s board, a fork, and play out
    /// the window it opens. Returns the board as it ends — possibly a further fork — or null when the
    /// event cannot happen there.
    /// </summary>
    public static BoardState Apply(ChangeEvent changeEvent, GameSituation situation, Faction valuer, int depth, List<StepOption> built)
    {
        if (changeEvent == null) return situation.Board;
        if (changeEvent.IsTrigger && depth < MaxDepth && ForecastsBlocks(valuer)
            && Block(changeEvent, situation, valuer, depth, built) is { } blocked)
            return blocked;
        if (!situation.Board.Apply(changeEvent, situation, cardTags: false)) return null;
        if (!changeEvent.IsTrigger || depth >= MaxDepth || !Forecasts(valuer)) return situation.Board;

        GameSituation window = situation.ReactingTo(changeEvent);
        HashSet<int> used = new();
        FactionTeam first = CardPlayRound.FirstTeamToReactTo(changeEvent);

        foreach (FactionTeam team in new[] { first, StaticGameData.OpponentTeam(first) })
            foreach (CardState card in Candidates(window, team, valuer).ToList())
            {
                if (used.Contains(card.Id) || !card.CardLogic.CanBeActivated(window)) continue;

                GameSituation tried = ProjectionValuer.PlaySteps(ProjectionValuer.Unfinished(card.CardLogic, window),
                    window.WithBoard(window.Board.Fork()), card.Faction, depth + 1, built);
                if (tried == null) continue;

                // Played only if its controller gains by it: our cards when they help us, theirs when they hurt us.
                if (ProjectionValuer.Value(tried.Board, card.Faction) <= ProjectionValuer.Value(window.Board, card.Faction)) continue;

                used.Add(card.Id);
                window = window.WithBoard(tried.Board);
                if (CliArgs.Verbose)
                    DebugUtilities.PrintPeer($"FORECAST +{card.CardName} ({card.Faction}) after {changeEvent.ScriptName}");
            }

        return window.Board;
    }

    /// <summary>
    /// The board as it would be if <paramref name="changeEvent"/> were blocked — a fork of
    /// <paramref name="situation"/>'s board with the best block card's cost paid — or null when nobody
    /// would block it. Only the team that did not cause the event may block, and nobody blocks their
    /// own action, as in CardPlayRound.RequestBlockReactions. Letting it through is valued as the event
    /// alone, without the reactions it would open.
    /// </summary>
    private static BoardState Block(ChangeEvent changeEvent, GameSituation situation, Faction valuer, int depth, List<StepOption> built)
    {
        FactionTeam team = StaticGameData.OpponentFactionTeamForFaction(changeEvent.TriggeringFaction);
        if (team == FactionTeam.NONE) return null;

        GameSituation window = situation.OfferingForBlock(changeEvent);
        List<CardState> blockers = Candidates(window, team, valuer, blocks: true)
            .Where(card => card.Faction != changeEvent.TriggeringFaction && card.CardLogic.CanBeActivated(window))
            .ToList();
        if (blockers.Count == 0) return null;

        BoardState through = situation.Board.Fork();
        if (!through.Apply(changeEvent, situation, cardTags: false)) return null;

        BoardState best = null;
        foreach (CardState card in blockers)
        {
            // PlaySteps skips the Block step itself: what is left is what the block costs.
            GameSituation tried = ProjectionValuer.PlaySteps(ProjectionValuer.Unfinished(card.CardLogic, window),
                window.WithBoard(situation.Board.Fork()), card.Faction, depth + 1, built);
            if (tried == null) continue;
            if (ProjectionValuer.Value(tried.Board, card.Faction) <= ProjectionValuer.Value(best ?? through, card.Faction)) continue;

            best = tried.Board;
            if (CliArgs.Verbose)
                DebugUtilities.PrintPeer($"FORECAST block {card.CardName} ({card.Faction}) stops {changeEvent.ScriptName}");
        }
        return best;
    }

    /// <summary>The team's cards on the table that the valuing faction can see and that could react to an event.</summary>
    private static IEnumerable<CardState> Candidates(GameSituation window, FactionTeam team, Faction valuer, bool blocks = false)
    {
        // While EnemyTurnValue measures, what the measuring faction knows: its own hidden Responses too.
        Faction knower = EnemyTurnValue.Measuring ? EnemyTurnValue.MeasuringFor : valuer;
        bool ownTeam = team == StaticGameData.FactionTeamForFaction(knower);
        foreach (Faction faction in StaticGameData.FactionsForTeam(team))
        {
            if (FactionState.ForEnum(faction) == null) continue;
            FactionRecord piles = window.Board.ForFaction(faction);

            foreach (CardState card in CardState.ForIds(piles.Status.Concat(piles.Response).ToList()))
            {
                CardLogic logic = card.CardLogic;
                if (logic == null || logic.IsBlockReaction != blocks || !logic.HasEventBasedTrigger) continue;
                bool visible = ownTeam || logic.IsStatus || window.Board.Of(card).IsRevealed;
                if (visible) yield return card;
            }
        }
    }
}
