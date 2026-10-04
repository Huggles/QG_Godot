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
/// Block windows are not forecast: a block cancels the event, which is a different shape of answer.
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

    /// <summary>
    /// Apply <paramref name="changeEvent"/> to <paramref name="situation"/>'s board, a fork, and play out
    /// the window it opens. Returns the board as it ends — possibly a further fork — or null when the
    /// event cannot happen there.
    /// </summary>
    public static BoardState Apply(ChangeEvent changeEvent, GameSituation situation, Faction valuer, int depth, List<StepOption> built)
    {
        if (changeEvent == null) return situation.Board;
        if (!situation.Board.Apply(changeEvent, situation, cardTags: false)) return null;
        if (!changeEvent.IsTrigger || depth >= MaxDepth || !Forecasts(valuer)) return situation.Board;

        GameSituation window = situation.ReactingTo(changeEvent);
        HashSet<int> used = new();
        FactionTeam first = CardPlayRound.FirstTeamToReactTo(changeEvent);

        foreach (FactionTeam team in new[] { first, StaticGameData.OpponentTeam(first) })
            foreach (CardState card in Candidates(window, team, valuer).ToList())
            {
                if (used.Contains(card.Id) || !card.CardLogic.CanBeActivated(window)) continue;

                GameSituation tried = ProjectionValuer.PlaySteps(card.CardLogic.CardSteps.Where(s => !s.StepFinished),
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

    /// <summary>The team's cards on the table that the valuing faction can see and that could react to an event.</summary>
    private static IEnumerable<CardState> Candidates(GameSituation window, FactionTeam team, Faction valuer)
    {
        bool ownTeam = team == StaticGameData.FactionTeamForFaction(valuer);
        foreach (Faction faction in StaticGameData.FactionsForTeam(team))
        {
            if (FactionState.ForEnum(faction) == null) continue;
            FactionRecord piles = window.Board.ForFaction(faction);

            foreach (CardState card in CardState.ForIds(piles.Status.Concat(piles.Response).ToList()))
            {
                CardLogic logic = card.CardLogic;
                if (logic == null || logic.IsBlockReaction || !logic.HasEventBasedTrigger) continue;
                bool visible = ownTeam || logic.IsStatus || window.Board.Of(card).IsRevealed;
                if (visible) yield return card;
            }
        }
    }
}
