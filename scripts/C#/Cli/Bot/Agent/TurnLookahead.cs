using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Values a card play as the best line of the faction's own next turns, not the play alone: Blitzkrieg
/// is worth nothing the turn it goes down and an Army the turn a land battle triggers it, and only a
/// line of turns sees the second half. <c>bot_lookahead=all|none|AXIS|ALLIES</c> turns it on, none
/// by default; <c>bot_lookahead_depth</c> is how many own turns the line spans (3) and
/// <c>bot_lookahead_beam</c> how many follow-ups each later turn explores (3).
///
/// Between two own turns the other factions pass and the board stands still, but for the faction's own
/// supply step: its units out of supply are removed. Nothing is drawn — the deck's order is hidden — so
/// a later turn plays from the hand that is left. Each turn's gain is valued as
/// <see cref="ProjectionValuer.Value(BoardState, Faction, int)"/> counts it, with income from that turn
/// on only, so a card that pays now still beats the same card played a turn later.
///
/// A Status or Response card is worth its enemy turns as well, with <c>bot_enemy_turns</c> on: see
/// <see cref="EnemyTurnValue"/>.
///
/// Only the faction's own play step looks ahead: a reaction during someone else's turn is valued as
/// before, by <see cref="ProjectionValuer.ValueCard"/>.
/// </summary>
public static class TurnLookahead
{
    public static int Depth => Math.Max(CliArgs.GetInt("bot_lookahead_depth", 3), 1);

    public static int Beam => Math.Max(CliArgs.GetInt("bot_lookahead_beam", 3), 1);

    /// <summary>Whether <paramref name="faction"/> values its play by a line right now.</summary>
    public static bool Applies(Faction faction)
    {
        if (!BotSides.Includes("bot_lookahead", "none", faction)) return false;
        BoardState live = BoardState.Live;
        return live.CurrentFaction == faction && live.TurnStep == TurnStep.PLAY_CARD
               && GameSituation.Live.ReactionDepth == 0;
    }

    /// <summary>The best line that opens with <paramref name="cardId"/>; null when the card cannot be valued.</summary>
    public static double? ValueLine(int cardId, Faction faction)
    {
        List<StepOption> built = new();
        try
        {
            Ply first = Play(cardId, ProjectionValuer.Start(), faction, 1, built);
            if (first == null) return null;

            (double rest, string path) = Depth > 1 ? Continue(first.After.Board, faction, 2, built) : (0, "");
            if (CliArgs.Verbose)
                DebugUtilities.PrintPeer($"LOOKAHEAD {faction} {CardState.ForId(cardId).CardName}{path} = {first.Gain + rest:0.##} ({first.Gain:0.##} now)");
            return first.Gain + rest;
        }
        finally
        {
            StepChoice.Release(built);
        }
    }

    /// <summary>One turn's play: the board after it, and what it gained over the board before.</summary>
    private sealed record Ply(int CardId, GameSituation After, double Gain);

    /// <summary>
    /// The best the faction's turns from <paramref name="ply"/> to <see cref="Depth"/> can add, after
    /// <paramref name="board"/>, and the cards that do it. Every candidate is valued on its own, then
    /// only the <see cref="Beam"/> best are followed further.
    /// </summary>
    private static (double Value, string Path) Continue(BoardState board, Faction faction, int ply, List<StepOption> built)
    {
        if (ply - 1 > RoundsLeft) return (0, "");
        BoardState next = NextOwnTurn(board, faction, built);
        if (next == null) return (0, "");

        GameSituation turn = GameSituation.NewTurnOn(next);
        List<Ply> plays = Candidates(turn, faction)
            .Select(id => Play(id, turn, faction, ply, built))
            .Where(p => p != null)
            // Passing keeps the board and costs a card from hand, or a VP from an empty one.
            .Append(new Ply(-1, turn, -ProjectionValuer.CardWorth(faction)))
            .OrderByDescending(p => p.Gain)
            .ToList();

        double best = double.NegativeInfinity;
        string bestPath = "";
        foreach (Ply play in ply < Depth ? plays.Take(Beam) : plays.Take(1))
        {
            (double rest, string path) = ply < Depth ? Continue(play.After.Board, faction, ply + 1, built) : (0, "");
            if (play.Gain + rest <= best) continue;
            best = play.Gain + rest;
            bestPath = $" -> {(play.CardId < 0 ? "pass" : CardState.ForId(play.CardId).CardName)}{path}";
        }
        return (best, bestPath);
    }

    /// <summary>
    /// <paramref name="cardId"/> played at the start of <paramref name="situation"/>, on a fork of its
    /// board. The gain is valued with turn <paramref name="ply"/>'s income horizon, and a card that
    /// leaves the hand is added back, as every play spends one.
    /// </summary>
    private static Ply Play(int cardId, GameSituation situation, Faction faction, int ply, List<StepOption> built)
    {
        CardState card = CardState.ForId(cardId);
        if (card?.CardLogic == null) return null;

        bool spends = situation.Board.ForFaction(card.Faction).Hand.Contains(cardId);
        GameSituation after = ProjectionValuer.ProjectCard(cardId, situation.WithBoard(situation.Board.Fork()), faction, true, built);
        if (after == null) return null;

        int horizon = Math.Max(RoundsLeft - (ply - 1), 1);
        double gain = ProjectionValuer.Value(after.Board, faction, horizon)
                      - ProjectionValuer.Value(situation.Board, faction, horizon)
                      + (spends ? ProjectionValuer.CardWorth(faction) : 0);

        // A card put on the table pays on enemy turns too, which this line of own turns never plays.
        if (spends && (card.CardLogic.IsStatus || card.CardLogic.IsResponse) && EnemyTurnValue.Applies(faction))
            gain += EnemyTurnValue.Of(cardId, faction, horizon);
        return new Ply(cardId, after, gain);
    }

    /// <summary>The cards the faction could play or activate at the start of <paramref name="turn"/>.</summary>
    private static IEnumerable<int> Candidates(GameSituation turn, Faction faction)
    {
        FactionRecord piles = turn.Board.ForFaction(faction);
        return CardState.ForIds(piles.Hand.Concat(piles.Status).Concat(piles.Response).ToList())
            .Where(card => card.CardLogic != null && card.CardLogic.CanBeActivated(turn))
            .Select(card => card.Id);
    }

    /// <summary>
    /// A fork of <paramref name="board"/> at the start of the faction's next turn: its supply step taken,
    /// everyone else passing. Null when the fork cannot get there.
    /// </summary>
    private static BoardState NextOwnTurn(BoardState board, Faction faction, List<StepOption> built)
    {
        BoardState next = board.Fork();
        GameSituation situation = GameSituation.NewTurnOn(next);

        foreach (int unitId in next.UnsuppliedUnitIds(faction))
        {
            RemoveUnitChangeEvent removal = new(faction, unitId, UnitRemovalReason.SUPPLY, next);
            built.Add(new StepOption(null, removal));
            if (!next.Apply(removal, situation, cardTags: false)) return null;
        }

        return MoveToTurnOf(next, faction, built) ? next : null;
    }

    /// <summary>
    /// A fork of <paramref name="board"/> moved on to <paramref name="faction"/>'s next turn, with
    /// everyone in between passing and nothing else changed. Null when the fork cannot get there.
    /// </summary>
    internal static BoardState TurnOf(BoardState board, Faction faction, List<StepOption> built)
    {
        BoardState next = board.Fork();
        return MoveToTurnOf(next, faction, built) ? next : null;
    }

    private static bool MoveToTurnOf(BoardState board, Faction faction, List<StepOption> built)
    {
        int turn = board.GameTurn + 1;
        for (int i = 0; i < StaticGameData.PlayableFactions.Count && StaticGameData.FactionForTurn(turn) != faction; i++) turn++;
        if (StaticGameData.FactionForTurn(turn) != faction) return false;

        ChangeRoundChangeEvent round = new(turn);
        built.Add(new StepOption(null, round));
        return board.Apply(round, GameSituation.NewTurnOn(board), cardTags: false);
    }

    /// <summary>Rounds still to score after this one; the line stops at the end of the game.</summary>
    private static int RoundsLeft
    {
        get
        {
            GameFlow flow = GameFlow.Instance;
            return flow == null ? 0 : Math.Max(flow.MaxRound - flow.Round, 0);
        }
    }
}
