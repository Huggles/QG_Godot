using System.Collections.Generic;

/// <summary>
/// A game in progress on disk: the base scenario, plus every <see cref="ChangeEvent"/> that has been
/// applied, in order.
///
/// Loading replays that log through the normal Apply() pipeline — broadcast on, animations off — so the
/// board, decks, victory points, card history, the history panel and every client's state all
/// reconstruct through the live code rather than through a parallel deserializer. Each event's DTO
/// carries its own outcome (the shuffled order, the cards discarded, the modifiers applied), so replay
/// is re-APPLYING decisions rather than re-MAKING them: it does not depend on the game being
/// deterministic, and a save survives most changes to card logic.
///
/// Two things are deliberately not in here:
///
/// • Player-to-faction ownership is not authoritative. <see cref="Seats"/> records who held what only so
///   the lobby can pre-fill it; players re-claim factions before the board is restored, and may pick
///   differently. Nothing about the replay depends on it — the host replays alone.
/// • The suspended call stack. When the save was taken the host was parked deep inside an await chain,
///   and nothing can serialize that. This is why saving is restricted to the two checkpoints in
///   <c>GameFlow.CanSave</c>, and why <see cref="PendingInput"/> is an assertion rather than an input.
/// </summary>
public class SaveGame
{
    public const int CurrentVersion = 1;

    /// <summary>Schema version. A save from a different version is refused rather than half-read.</summary>
    public int Version { get; set; } = CurrentVersion;

    public string SavedAtIso { get; set; }

    /// <summary>Shown in the load list, e.g. "Standard Game — Round 4, Japan".</summary>
    public string DisplayName { get; set; }

    // ── Setup: self-contained ───────────────────────────────────────────────

    /// <summary>
    /// The scenario itself, base64 of the UTF-8 text the running session was built from — not a path.
    /// A save therefore does not care whether the original file still exists, is still at the same
    /// path, or still has the same contents.
    /// </summary>
    public string ScenarioJsonBase64 { get; set; }

    /// <summary>Display only. Nothing resolves this against the locally available scenarios.</summary>
    public string ScenarioTitle { get; set; }

    /// <summary>Informational, for bug reports. Nothing resolves it, and it may be null.</summary>
    public string ScenarioPath { get; set; }

    /// <summary>The RESOLVED value in force, after any lobby override — not the scenario's own.</summary>
    public bool OpeningDiscard { get; set; }

    public int Seed { get; set; }

    /// <summary>
    /// GameRandom.DrawCount at save time. Re-seeding and then fast-forwarding this many draws puts the
    /// restored game's randomness where the saved one left it, rather than handing the continuation the
    /// numbers the opening shuffle already used.
    /// </summary>
    public int RngDraws { get; set; }

    // ── The payload ─────────────────────────────────────────────────────────

    /// <summary>
    /// The ordered ChangeEvent log — the whole reconstruction source, replayed in sequence. It already
    /// contains every setup event (deck order, deploys, initial cards, starting VP, round and step
    /// changes), which is why no scenario setup runs on load.
    ///
    /// PresentationEvents are not here: they mutate nothing and a replay suppresses animations anyway.
    /// Neither are RecalculateTagsMessages, which are derived state the host recomputes per event.
    ///
    /// Typed as GameMessageDto even though every element is a ChangeEventDto, and that is load-bearing:
    /// the [JsonPolymorphic] configuration lives on GameMessageDto, and System.Text.Json resolves
    /// polymorphism from the type it is handed. Declaring this List&lt;ChangeEventDto&gt; would write every
    /// event with no "$type" discriminator and make the save unreadable. Same footgun as
    /// GameMessage.BroadCast, which passes typeof(GameMessageDto) by hand for exactly this reason.
    /// </summary>
    public List<GameMessageDto> Events { get; set; } = new();

    // ── Live engine state that no ChangeEvent reconstructs ──────────────────

    public GameFlowSnapshot Flow { get; set; }

    // ── Resume and verification ─────────────────────────────────────────────

    public ResumeMode Resume { get; set; }

    /// <summary>
    /// The prompt that was open when the save was taken, or null if the game was between actions.
    ///
    /// NOT injected on load — it cannot be, because the await that was waiting for it is gone. It is
    /// stored so the resumed step, which re-issues its own opening prompt, can be checked against the
    /// one that was actually open. A mismatch means the resume landed somewhere other than where the
    /// save was taken, and that is worth a line in the log.
    /// </summary>
    public PendingPrompt PendingInput { get; set; }

    /// <summary>MultiplayerGameState.ComputeHash() at save time, compared after replay.</summary>
    public string StateHash { get; set; }

    /// <summary>Who held which factions, for lobby pre-fill only. Never enforced.</summary>
    public List<SavedSeat> Seats { get; set; } = new();
}

/// <summary>Turn-engine state that lives on <see cref="GameFlow"/> and is not derivable from the log.</summary>
public class GameFlowSnapshot
{
    public bool GameStarted { get; set; }
    public int GameTurn { get; set; }

    /// <summary>
    /// The step counter to restore. NOT simply the live value: it is stored one lower for
    /// <see cref="ResumeMode.ReRunCurrentStep"/> so that a single resume mechanism — advance one step —
    /// lands correctly for both resume modes. See GameFlow.SaveStepCounter.
    /// </summary>
    public int TurnStepCounter { get; set; }

    public TurnStep TurnStep { get; set; }
    public int MaxRound { get; set; }

    public Dictionary<Faction, int> CardsPlayedThisTurnStep { get; set; } = new();
    public Dictionary<Faction, List<VPTurnSummary>> VictoryPointSummaries { get; set; } = new();

    /// <summary>
    /// The scoped reaction skips. Host-only and self-expiring, and they decide whether a reaction
    /// window opens at all — so a restore that dropped them would start asking a player questions they
    /// had explicitly opted out of for the rest of the step or round.
    /// </summary>
    public Dictionary<Faction, ReactionSkipWindow> ReactionSkipTurnStep { get; set; } = new();
    public Dictionary<Faction, int> ReactionSkipRound { get; set; } = new();
}

/// <summary>Serializable form of GameFlow's <c>(int Turn, TurnStep Step)</c> skip window.</summary>
public class ReactionSkipWindow
{
    public int Turn { get; set; }
    public TurnStep Step { get; set; }
}

/// <summary>How the turn machine picks up after a restore.</summary>
public enum ResumeMode
{
    /// <summary>The saved step had finished. Advance to the next one.</summary>
    NextStep,

    /// <summary>
    /// The save was taken on a step's opening prompt, before anything happened in it. Re-run the step's
    /// body — not BeginStep — so it re-issues that prompt.
    /// </summary>
    ReRunCurrentStep
}

/// <summary>An open input request, reduced to what a save needs to recognise it again.</summary>
public class PendingPrompt
{
    /// <summary>The request class name without its "RequestHandler" suffix, e.g. "HandCardPlay".</summary>
    public string Kind { get; set; }

    public Faction Faction { get; set; }

    public static string KindOf(InputRequest request)
    {
        string name = request.GetType().Name;
        const string suffix = "RequestHandler";
        return name.EndsWith(suffix) ? name.Substring(0, name.Length - suffix.Length) : name;
    }

    public bool Matches(InputRequest request)
        => Kind == KindOf(request) && Faction == request.TargetFaction;

    public override string ToString() => $"{Kind} for {Faction}";
}

/// <summary>One player's seat in the saved game, for lobby pre-fill.</summary>
public class SavedSeat
{
    /// <summary>The name as the host listed it, so a returning player can be matched back to their seat.</summary>
    public string DisplayName { get; set; }

    public List<Faction> Factions { get; set; } = new();
}

/// <summary>Header for the load list, so browsing does not mean holding every event log in memory.</summary>
public class SaveGameMeta
{
    public string FilePath { get; set; }
    public string DisplayName { get; set; }
    public string SavedAtIso { get; set; }
    public string ScenarioTitle { get; set; }
    public int Version { get; set; }
    public int EventCount { get; set; }
}
