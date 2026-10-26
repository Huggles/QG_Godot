using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// What a finished ranked game is worth to each seat. The host settles it once at game end; every peer
/// then writes only its own change, because Steam lets a client write no leaderboard entry but its own.
/// </summary>
public static class RankedRating
{
    public const int WinPoints           = 10;
    public const int BonusPoints         = 1;
    public const int MinDiscardsForBonus = 3;

    /// <summary>Leads every game tag, so an entry seeded with no details can never read as one.</summary>
    private const int TagMarker = 0x51475231;

    private sealed class SeatTally
    {
        public int SeatId;
        public FactionTeam Team;
        public int Points, BestTurn, Eliminations, EnemyDiscards;
    }

    public static string NewGameId() => Guid.NewGuid().ToString("N");

    /// <summary>Host-only: a Steam lobby of at least two people, no bots, base game.</summary>
    public static bool SessionQualifies(MultiplayerApi multiplayer)
        => SteamworksApi.IsAvailable
           && SteamPeerFactory.IsSteamTransport(multiplayer.MultiplayerPeer)
           && GameOptions.EnabledExpansions.Count == 0
           && PlayerFactionRegistry.GetHumanSeatIds().Count >= 2
           && PlayerFactionRegistry.GetAiFactions().Count == 0;

    /// <summary>Host-only. A failure here unranks the game rather than stopping it from ending.</summary>
    public static void Settle(GameResult result, GameFlow flow, IReadOnlyList<GameMessage> journal)
    {
        result.GameId = flow.GameId;
        result.Ranked = flow.IsRanked && flow.Program == null;
        if (!result.Ranked) return;

        try
        {
            result.RatingChanges = Calculate(result, flow.VictoryPointSummaries, journal);
            foreach (RatingChange change in result.RatingChanges)
                DebugUtilities.PrintPeer($"Ranked: {change.DisplayName} {change.Delta:+0;-0} ({string.Join(", ", change.Reasons)})");
        }
        catch (Exception e)
        {
            DebugUtilities.PrintPeerErrorRaw($"Ranked: could not settle game {flow.GameId}, leaving it unranked: {e}");
            result.Ranked = false;
            result.RatingChanges = new List<RatingChange>();
        }
    }

    /// <summary>Every peer: starts the write of this player's own change and returns it for display.</summary>
    public static RatingChange SubmitLocal(GameResult result)
    {
        if (!result.Ranked || !Guid.TryParse(result.GameId, out Guid gameId)) return null;

        int seatId = PlayerFactionRegistry.GetLocalPeerId();
        RatingChange change = result.RatingChanges?.FirstOrDefault(c => c.SeatId == seatId);
        if (change == null || !SteamworksApi.IsAvailable) return change;

        int[] tag = TagFor(gameId);
        Guard.FireAndForget(() => SteamworksApi.Instance.SubmitScoreChangeAsync(
            LeaderboardDialog.LeaderboardName, change.Delta, LeaderboardDialog.StartingElo, tag), "RankedRating.SubmitLocal");
        return change;
    }

    private static int[] TagFor(Guid gameId)
    {
        byte[] bytes = gameId.ToByteArray();
        return new[]
        {
            TagMarker,
            BitConverter.ToInt32(bytes, 0), BitConverter.ToInt32(bytes, 4),
            BitConverter.ToInt32(bytes, 8), BitConverter.ToInt32(bytes, 12),
        };
    }

    private static List<RatingChange> Calculate(GameResult result,
        Dictionary<Faction, List<VPTurnSummary>> summaries, IReadOnlyList<GameMessage> journal)
    {
        Dictionary<Faction, SeatTally> byFaction = new();
        List<SeatTally> seats = new();
        foreach (int seatId in PlayerFactionRegistry.GetSeatIds())
        {
            List<Faction> factions = PlayerFactionRegistry.GetFactionsForPeerId(seatId);
            if (factions.Count == 0) continue;

            // The lobby keeps every seat on one team, so any of its factions names it.
            SeatTally seat = new() { SeatId = seatId, Team = StaticGameData.FactionTeamForFaction(factions[0]) };
            seats.Add(seat);
            foreach (Faction faction in factions) byFaction[faction] = seat;
        }

        SeatTally SeatFor(Faction faction) => byFaction.GetValueOrDefault(StaticGameData.ControllerOf(faction));

        foreach ((Faction faction, List<VPTurnSummary> scored) in summaries)
        {
            if (SeatFor(faction) is not { } seat || scored.Count == 0) continue;
            seat.Points  += scored.Sum(s => s.TotalScore);
            seat.BestTurn = Math.Max(seat.BestTurn, scored.GroupBy(s => s.TurnNumber).Max(turn => turn.Sum(s => s.TotalScore)));
        }

        foreach (RemoveUnitChangeEvent removal in journal.OfType<RemoveUnitChangeEvent>())
            if (IsAgainstEnemy(removal.TriggeringFaction, removal.UnitState.Faction) && SeatFor(removal.TriggeringFaction) is { } seat)
                seat.Eliminations++;

        foreach (ChangeEvent change in journal.OfType<ChangeEvent>())
        {
            int discarded = change switch
            {
                ForceDiscardCardsChangeEvent deck     => deck.DiscardedCardIds.Count,
                ForceDiscardHandCardsChangeEvent hand => hand.DiscardedCardIds.Count,
                DiscardHandCardsChangeEvent hand      => hand.CardIds?.Count ?? 0,
                _                                     => 0,
            };
            if (discarded > 0 && IsAgainstEnemy(change.TriggeringFaction, change.TargetFaction) && SeatFor(change.TriggeringFaction) is { } seat)
                seat.EnemyDiscards += discarded;
        }

        int bestTurnOverall = seats.Max(s => s.BestTurn);
        return seats.Select(seat =>
        {
            List<SeatTally> team = seats.Where(s => s.Team == seat.Team).ToList();
            bool won = !result.IsDraw && seat.Team == result.WinningTeam;
            RatingChange change = new()
            {
                SeatId      = seat.SeatId,
                DisplayName = PlayerFactionRegistry.GetDisplayNameForPeer(seat.SeatId),
                Team        = seat.Team,
                Delta       = result.IsDraw ? 0 : won ? WinPoints : -WinPoints,
            };
            change.Reasons.Add(result.IsDraw ? "Draw" : won ? "Victory" : "Defeat");

            void Bonus(bool earned, string reason)
            {
                if (!earned) return;
                change.Delta += BonusPoints;
                change.Reasons.Add(reason);
            }

            // Ties share a bonus: everyone level at the top earns it.
            Bonus(IsTop(seat, team, s => s.Points, 1),                          "top scorer on your team");
            Bonus(seat.BestTurn > 0 && seat.BestTurn == bestTurnOverall,         "best single turn");
            Bonus(IsTop(seat, team, s => s.Eliminations, 1),                     "most eliminations on your team");
            Bonus(IsTop(seat, team, s => s.EnemyDiscards, MinDiscardsForBonus), "most enemy discards on your team");
            return change;
        }).ToList();
    }

    private static bool IsTop(SeatTally seat, List<SeatTally> team, Func<SeatTally, int> stat, int minimum)
        => stat(seat) >= minimum && stat(seat) == team.Max(stat);

    private static bool IsAgainstEnemy(Faction acting, Faction affected)
    {
        FactionTeam actingTeam = StaticGameData.FactionTeamForFaction(acting);
        FactionTeam affectedTeam = StaticGameData.FactionTeamForFaction(affected);
        return actingTeam != affectedTeam && StaticGameData.OpponentTeam(actingTeam) == affectedTeam;
    }
}
