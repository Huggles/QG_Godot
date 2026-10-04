using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Host-only: turns the running game into a <see cref="SaveGame"/> and writes it through
/// <see cref="SaveGameService"/>. The other half, rebuilding a game from one, is <see cref="SessionRestore"/>.
/// </summary>
public static class SaveCapture
{
    /// <summary>
    /// Capture the game to disk and return the file path, or null if it could not be taken.
    ///
    /// Everything here must be read synchronously, in one go, before anything is awaited. The escape
    /// menu deliberately does not pause the tree (ErrorPopup documents why), so the turn loop keeps
    /// running while the menu is open — but it runs on this same thread, so a straight-line capture is
    /// atomic with respect to it. Await first and the log grows underneath you, leaving a save whose
    /// events, flow snapshot and hash describe three different moments.
    /// </summary>
    /// <param name="deferred">
    /// True when this is a save the player asked for at an unsaveable moment and GameFlow has just
    /// flushed at the next boundary. Only affects the log line and the toast.
    /// </param>
    public static string Capture(string displayName, bool deferred = false)
    {
        MultiplayerSession session = MultiplayerSession.Instance;
        if (session == null || !session.Multiplayer.IsServer())
        {
            DebugUtilities.PrintPeerError("CaptureSave: only the host can save the game");
            return null;
        }

        if (!GameFlow.Instance.CanSave)
        {
            DebugUtilities.PrintPeerErrorRaw($"CaptureSave: cannot save right now — {GameFlow.Instance.SaveBlockedReason}");
            return null;
        }

        SaveGame save;
        try
        {
            save = Build(session.GameState, displayName);
        }
        catch (Exception e)
        {
            // Raw, and with the trace: a save that throws while assembling is a bug in a ToDto somewhere,
            // and the one thing the player must not get is a silent no-op.
            DebugUtilities.PrintPeerErrorRaw($"CaptureSave: failed to assemble the save: {e}");
            return null;
        }

        string path = SaveGameService.Save(save);

        if (path != null)
            EventBus.Emit(EventBus.SignalName.GameSaved, displayName, deferred);

        return path;
    }

    /// <summary>
    /// Write the finished game as a <see cref="SaveGame.Completed"/> record. Skips the CanSave gate,
    /// since nothing will resume it, and the toast, since the victory screen is already the news.
    /// </summary>
    public static string CaptureCompleted(GameResult result)
    {
        MultiplayerSession session = MultiplayerSession.Instance;
        if (session == null || !session.Multiplayer.IsServer()) return null;

        string scenario = GameManager.ActiveScenarioTitle ?? "Game";
        bool axisWon = result.WinningTeam == FactionTeam.AXIS;
        string displayName = $"{scenario} — Completed, {result.WinningTeam.ToString().Capitalize()} win " +
                             $"{(axisWon ? result.AxisTotal : result.AlliesTotal)}-{(axisWon ? result.AlliesTotal : result.AxisTotal)} " +
                             $"(Round {result.FinalRound})";
        try
        {
            SaveGame save = Build(session.GameState, displayName);
            save.Completed = true;
            return SaveGameService.Save(save);
        }
        catch (Exception e)
        {
            // Never fatal: the game is over either way, and the victory screen must still come up.
            DebugUtilities.PrintPeerErrorRaw($"CaptureSave: failed to write the completed game: {e}");
            return null;
        }
    }

    /// <summary>The synchronous half of <see cref="Capture"/>. See its remarks on why.</summary>
    private static SaveGame Build(MultiplayerGameState gameState, string displayName)
    {
        return new SaveGame
        {
            // UTC with the ISO "T" separator, so an ordinal string sort is a chronological sort and the
            // load list needs no parsing to order itself. Rendered in local time for display.
            SavedAtIso   = Time.GetDatetimeStringFromSystem(utc: true),
            DisplayName  = displayName,

            // The scenario travels inside the save rather than as a path, so restoring does not depend
            // on the file still existing, still being at that path, or still having the same contents.
            ScenarioJsonBase64 = GameManager.ActiveScenarioJson == null
                ? null
                : Convert.ToBase64String(Encoding.UTF8.GetBytes(GameManager.ActiveScenarioJson)),
            ScenarioTitle  = GameManager.ActiveScenarioTitle,
            ScenarioPath   = GameManager.PendingScenarioPath,
            OpeningDiscard = GameFlow.Instance.OpeningDiscardEnabled,
            Seed           = GameRandom.Seed,
            RngDraws       = GameRandom.DrawCount,

            // OfType, not a cast: the journal also holds PresentationEvents (which mutate nothing) and
            // RecalculateTagsMessages (derived state the host recomputes per event during replay).
            Events = gameState.GameMessages.OfType<ChangeEvent>()
                              .Select(changeEvent => (GameMessageDto)changeEvent.ToDto())
                              .ToList(),

            Flow         = GameFlow.Instance.BuildFlowSnapshot(),
            Resume       = GameFlow.Instance.IsAtStepStart
                               ? ResumeMode.ReRunCurrentStep
                               : ResumeMode.NextStep,
            PendingInput = NetworkApi.Instance.DescribePendingInputs().FirstOrDefault(),
            StateHash    = gameState.ComputeHash(),
            Seats        = BuildSeats()
        };
    }

    /// <summary>
    /// Who held which factions, for the lobby to pre-fill on load. Never enforced — players re-claim
    /// factions before the board is restored, and may reshuffle freely.
    /// </summary>
    private static List<SavedSeat> BuildSeats()
    {
        // Built from the faction-to-peer map rather than from the registry's PlayerScene objects. Those
        // are Godot nodes held in a static that outlives the game scene, so reading one can throw
        // ObjectDisposedException — and a stale node must never be able to fail a save. The map is plain
        // dictionaries, and the display name is looked up through the registry's own null-safe accessor.
        return StaticGameData.PlayableFactions
            .GroupBy(PlayerFactionRegistry.GetPeerIdForFaction)
            .OrderBy(group => group.Key)
            .Select(group => new SavedSeat
            {
                DisplayName = NameForSeat(group),
                Factions    = group.ToList()
            })
            .ToList();
    }

    private static string NameForSeat(IEnumerable<Faction> factions)
    {
        // GetDisplayNameForFaction answers null for a freed or unnamed player, so an unclaimed seat
        // simply goes in unnamed and matches by join order on load.
        foreach (Faction faction in factions)
        {
            string name = PlayerFactionRegistry.GetDisplayNameForFaction(faction);
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }
        return null;
    }
}
