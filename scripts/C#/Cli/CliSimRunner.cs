using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Drives one unattended full game and reports its outcome as a single line. Entered from
/// <see cref="CliSession"/> when <c>sim=true</c>.
///
/// The point is volume: run this a few thousand times with a fixed <c>seed</c> and a varying
/// <c>decision_seed</c> and the result lines aggregate into balance statistics — who won, what each
/// faction scored, how long the game ran.
///
/// A sim run answers to nothing on stdin, so it needs its own ending. There are exactly three, and
/// keeping them distinguishable is the whole reliability story of a batch:
///   0 — the game reached a win condition and a result was emitted
///   2 — the game ended but rules errors were seen along the way (CliSession's own exit rule)
///   3 — nothing happened for a long time and no result ever arrived (see the watchdog)
/// A run that hangs instead of exiting 3 would stall a batch behind it, which is why the watchdog is
/// not optional.
/// </summary>
public sealed class CliSimRunner
{
    /// <summary>
    /// Frames of total silence — no ChangeEvent, no prompt — before a run is called stalled.
    ///
    /// Generous because it must never fire on a slow-but-live game: the cost of being wrong is a
    /// discarded run, and a game that is genuinely progressing resets this on every applied event
    /// through CliSession.MarkBusy. Override with <c>sim_stall_frames</c>.
    /// </summary>
    private const int DefaultStallFrames = 3000;

    private readonly CliSession _session;
    private readonly CliRenderer _renderer;
    private readonly RandomInputProvider _bot;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private readonly int _decisionSeed;
    private readonly int _stallFrames;

    /// <summary>
    /// Press Continue automatically for recoverable failures. On by default; <c>sim_resume=false</c>
    /// turns it off, which is what you want when hunting one specific failure — the run then stops at
    /// the first one with the board still standing, instead of carrying on past it.
    /// </summary>
    private readonly bool _autoResume;

    private int _frames;
    private int _resumes;
    private bool _finished;
    private bool _subscribed;

    public CliSimRunner(CliSession session, CliRenderer renderer)
    {
        _session = session;
        _renderer = renderer;

        _decisionSeed = CliArgs.GetInt("decision_seed", 0);
        _stallFrames = CliArgs.GetInt("sim_stall_frames", DefaultStallFrames);
        _autoResume = CliArgs.GetBool("sim_resume", true);

        double passChance = Probability("bot_pass");
        double discardChance = Probability("bot_discard");
        double hollowChance = Probability("bot_hollow");

        List<IBotRule> rules = BotRuleRegistry.All();
        string kindError = BotRuleRegistry.ValidateKinds(rules);
        Dictionary<string, BotRuleConfig> ruleConfig = kindError != null
            ? null
            : BotRuleRegistry.Parse(CliArgs.Get("bot_rules"), rules, out kindError);

        // Before the game, not during it. A misspelled rule name that only surfaced as "no effect"
        // would let a batch of thousands of games quietly measure the default policy and answer a
        // question nobody asked — which is worse than a crash, because the number it produces looks
        // real. Exit 2 puts the run in failures.txt with its own arguments attached.
        if (ruleConfig == null)
        {
            renderer.Emit(new CliEvent("bot_config_error")
                .Set("error", kindError)
                .Text($"BOT CONFIG ERROR  {kindError}"));
            _session.Quit(2);
            ruleConfig = BotRuleRegistry.Parse(null, rules, out _);
        }
        else if (hollowChance > 0)
        {
            // bot_hollow keeps its name: it is what the verified 200-game baseline was run with, and
            // it appears in SimJob.ToUserArgs and the aggregator's summary header. It is now exactly
            // sugar for no_hollow:suppress=X.
            if (CliArgs.Has("bot_rules") && CliArgs.Get("bot_rules").Contains("suppress"))
            {
                renderer.Emit(new CliEvent("bot_config_error")
                    .Set("error", "bot_hollow and an explicit suppress= are the same setting")
                    .Text("BOT CONFIG ERROR  bot_hollow=X is sugar for bot_rules=no_hollow:suppress=X " +
                          "— set one or the other, not both"));
                _session.Quit(2);
            }
            ruleConfig["no_hollow"].Suppress = hollowChance;
        }

        _bot = new RandomInputProvider(_decisionSeed, passChance, discardChance,
            rules, ruleConfig, Probability("bot_rule_temp"),
            CliArgs.GetInt("bot_yield_every", 64), CliArgs.Verbose ? renderer : null);

        // Replaces the CliInputProvider CliSession would otherwise install: nothing is reading stdin
        // for answers, so every prompt has to be resolved by the bot or the run parks forever.
        InputServices.Override(_bot);

        EventBus.Instance.GameEnded += OnGameEnded;
        _subscribed = true;

        _renderer.Emit(new CliEvent("sim_started")
            .Set("seed", GameRandom.Seed)
            .Set("decision_seed", _decisionSeed)
            .Set("bot_pass", passChance)
            .Set("bot_discard", discardChance)
            .Set("bot_hollow", hollowChance)
            // The active rule set, not the raw argument: with defaults and sugar both in play, the raw
            // string does not say what actually ran, and this line is the run's self-description.
            .Set("bot_rules", string.Join(",", ActiveRuleNames()))
            .Text($"SIM  decision_seed {_decisionSeed}, bot_pass {passChance}, bot_discard {discardChance}, " +
                  $"bot_hollow {hollowChance}, bot_rules [{string.Join(" ", ActiveRuleNames())}]"));
    }

    /// <summary>Names of the rules actually enabled this run, in registry order.</summary>
    private IEnumerable<string> ActiveRuleNames() => _bot.Engine.Rules
        .Where(rule => _bot.Engine.ConfigFor(rule.Name).Enabled)
        .Select(rule => rule.Name);

    /// <summary>
    /// Per-rule counters for the game just finished.
    ///
    /// Emitted with the game facts and BEFORE sim_perf for the reason stated there: it is reproducible
    /// from (seed, decision_seed) plus the rule configuration, so it belongs on the diffable side of
    /// the line and not with the wall-clock.
    ///
    /// Every REGISTERED rule is reported, including the disabled ones at all zeroes — the opposite of
    /// card_stats, which omits the cards nothing happened to. With ten rules rather than a hundred and
    /// twenty cards the size argument does not apply, and stable columns are worth more: two batches
    /// run with different rule sets have to line up in one spreadsheet.
    /// </summary>
    private void EmitBotRuleStats()
    {
        BotRuleEngine engine = _bot.Engine;
        List<object> rows = new();

        foreach (IBotRule rule in engine.Rules)
        {
            BotRuleConfig config = engine.ConfigFor(rule.Name);
            BotRuleStats stats = engine.StatsFor(rule.Name);
            rows.Add(new Dictionary<string, object>
            {
                ["name"] = rule.Name,
                ["enabled"] = config.Enabled,
                ["weight"] = config.Weight,
                ["suppress"] = config.Suppress,
                ["considered"] = stats.Considered,
                ["fired"] = stats.Fired,
                ["vetoed"] = stats.Vetoed,
                ["scored"] = stats.Scored,
                ["forced_pass"] = stats.ForcedPass,
                ["refused_pass"] = stats.RefusedPass,
                ["suppressed"] = stats.Suppressed,
                ["floored"] = stats.Floored,
                ["failed"] = stats.Failed,
            });
        }

        _renderer.Emit(new CliEvent("bot_rules_stats")
            .Set("seed", GameRandom.Seed)
            .Set("decision_seed", _decisionSeed)
            .Set("prompts", _bot.Answered)
            .Set("passed", _bot.Passed)
            .Set("safety_overrides", engine.SafetyOverrides)
            .Set("rules", rows)
            .Text($"RULES  {engine.SafetyOverrides} safety override(s); " +
                  string.Join("  ", engine.Rules
                      .Where(r => engine.ConfigFor(r.Name).Enabled)
                      .Select(r => {
                          BotRuleStats s = engine.StatsFor(r.Name);
                          return $"{r.Name} fired={s.Fired}/{s.Considered} vetoed={s.Vetoed}" +
                                 (s.Floored > 0 ? $" FLOORED={s.Floored}" : "") +
                                 (s.Failed > 0 ? $" FAILED={s.Failed}" : "");
                      }))));
    }

    /// <summary>
    /// A 0..1 argument. Invariant culture explicitly, so <c>bot_pass=0.2</c> means the same thing on a
    /// machine with a comma decimal separator as it does in the scripts that write it.
    /// </summary>
    private static double Probability(string key)
        => double.TryParse(CliArgs.Get(key, "0"), System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out double parsed)
            ? Math.Clamp(parsed, 0, 1)
            : 0;

    /// <summary>
    /// Called once per frame from <see cref="CliSession._Process"/>. Only the watchdog lives here —
    /// the run itself is driven entirely by the game loop.
    /// </summary>
    public void Tick()
    {
        _frames++;
        if (_finished) return;
        if (TryResumeAfterRecoverableFailure()) return;
        if (_session.QuietFrames < _stallFrames) return;

        _finished = true;
        GameFlow flow = GameFlow.Instance;
        _renderer.Emit(new CliEvent("sim_stalled")
            .Set("decision_seed", _decisionSeed)
            .Set("turn", flow?.GameTurn ?? -1)
            .Set("round", flow?.Round ?? -1)
            .Set("step", flow?.TurnStep.ToString() ?? "?")
            .Set("prompts", _bot.Answered)
            .Set("frames", _frames)
            .Text($"STALLED after {_stallFrames} quiet frame(s) at turn {flow?.GameTurn}, " +
                  $"round {flow?.Round}, step {flow?.TurnStep} ({_bot.Answered} prompt(s) answered)"));

        // Deliberately not 0/1/2: a stall is neither a result nor a rules error, and a batch that
        // cannot tell them apart would silently count hung runs as losses for whoever was behind.
        _session.Quit(3);
    }

    /// <summary>
    /// Press Continue for the absent player, and report whether it was pressed.
    ///
    /// A Recoverable failure parks the turn loop on a popup offering Continue — that is the design
    /// (see GameRuleException, and UnitPool.RecallCandidates, which knowingly leaves one such case in
    /// on the strength of it). Headless there is no popup and nobody to click it, so without this the
    /// loop would sit there until the watchdog killed the run, and a whole class of ordinary rule
    /// refusals would read as a stalled game.
    ///
    /// Recoverable ONLY. An Unrecoverable stall means state was mutated but never replicated, so the
    /// board can no longer be vouched for; resuming would still produce a result line, which is worse
    /// than producing none. Those fall through to the watchdog and end the run at exit 3.
    ///
    /// The run is still not clean — CliSession.ErrorsSeen counts the failure, so the result line
    /// carries errors > 0 and the process exits 2. A batch therefore gets the game's outcome AND the
    /// seed pair needed to reproduce the bug, instead of having to choose between them.
    /// </summary>
    private bool TryResumeAfterRecoverableFailure()
    {
        if (!_autoResume) return false;

        ErrorReporter reporter = ErrorReporter.Instance;
        if (reporter == null || !reporter.HasPendingStall) return false;
        if (reporter.PendingSeverity != ErrorSeverity.Recoverable) return false;

        _resumes++;
        GameFlow flow = GameFlow.Instance;
        _renderer.Emit(new CliEvent("sim_resumed")
            .Set("decision_seed", _decisionSeed)
            .Set("turn", flow?.GameTurn ?? -1)
            .Set("round", flow?.Round ?? -1)
            .Set("step", flow?.TurnStep.ToString() ?? "?")
            .Text($"RESUME after a recoverable failure at turn {flow?.GameTurn}, " +
                  $"round {flow?.Round}, step {flow?.TurnStep} (resume #{_resumes})"));

        ErrorReporter.RequestResume();
        return true;
    }

    /// <summary>
    /// Emit the outcome and stop. Ends the process before MultiplayerSession finishes its drain and
    /// switches to the victory screen — there is no screen here, and the result is already final.
    /// </summary>
    private void OnGameEnded(string resultJson)
    {
        if (_finished) return;
        _finished = true;

        GameResult result;
        try
        {
            result = JsonSerializer.Deserialize<GameResult>(resultJson);
        }
        catch (Exception e)
        {
            ErrorReporter.Report(e, "CliSimRunner.OnGameEnded");
            _session.Quit(2);
            return;
        }

        // A flat faction -> score map rather than the GameResult itself: FactionResult carries
        // FactionData (labels, colours, flag paths) that a statistics run has no use for, and at
        // thousands of lines the difference is the whole file size.
        Dictionary<string, object> factions = new();
        foreach (FactionResult faction in result.Factions)
            factions[faction.Faction.ToString()] = faction.Total;

        _renderer.Emit(new CliEvent("game_result")
            .Set("seed", GameRandom.Seed)
            .Set("decision_seed", _decisionSeed)
            .Set("scenario", GameManager.PendingScenarioPath)
            .Set("winner", result.WinningTeam.ToString())
            .Set("axis", result.AxisTotal)
            .Set("allies", result.AlliesTotal)
            .Set("final_round", result.FinalRound)
            .Set("end_reason", result.EndReason)
            .Set("factions", factions)
            .Set("prompts", _bot.Answered)
            .Set("passed", _bot.Passed)
            // The BOT's draw count, distinct from rng_draws below, which is GameRandom's. Two runs of
            // one seed pair must agree on this; when they do not, the decision stream diverged and this
            // says by how much. See RandomInputProvider.Draws.
            .Set("bot_draws", _bot.Draws)
            .Set("errors", _session.ErrorsSeen)
            .Set("resumes", _resumes)
            .Set("rng_draws", GameRandom.DrawCount)
            .Text($"RESULT {result.WinningTeam} wins {result.AxisTotal}-{result.AlliesTotal} " +
                  $"after round {result.FinalRound} ({result.EndReason})  " +
                  $"[{_bot.Answered} prompts]"));

        EmitRoundScores(result);
        EmitCardStats();
        EmitBotRuleStats();

        // Separate event, deliberately. Everything above is a statement about the GAME and is
        // reproducible from (seed, decision_seed) alone, so two runs of the same pair produce
        // byte-identical game_result lines and a batch can be diffed as a regression baseline.
        // Wall-clock never reproduces, and mixing it in would have made that diff always fail.
        // Frames and milliseconds sit together because they answer different questions: whether a run
        // is slow because the loop does a lot, or merely because it waits a lot.
        _renderer.Emit(new CliEvent("sim_perf")
            .Set("decision_seed", _decisionSeed)
            .Set("frames", _frames)
            .Set("elapsed_ms", (int)_clock.ElapsedMilliseconds)
            .Text($"PERF  {_frames} frame(s), {_clock.ElapsedMilliseconds} ms"));

        _session.Quit(0);
    }

    /// <summary>
    /// Each faction's victory points round by round: what it scored in the round, and what it stood at
    /// when the round closed.
    ///
    /// A separate event from <c>game_result</c> on purpose. The result line is one row per game and
    /// gets read by eye; this is <c>factions x rounds</c> numbers and would have swamped it — the same
    /// reasoning that keeps FactionData out of the result's faction map. It is still reproducible from
    /// (seed, decision_seed) like everything above, so it belongs before <c>sim_perf</c>, not after.
    ///
    /// Shape is one array per faction indexed by round 1..FinalRound, rather than a list of per-round
    /// objects: a batch aggregating hundreds of games wants to add column N to a running total, and
    /// that read should not cost a dictionary lookup and a null check per round.
    ///
    /// Zero-filled, which the source data is not — GameFlow builds PerRound by grouping the rounds a
    /// faction actually has a VP summary for, so a round it did not score in is simply absent. Left as
    /// gaps these would average as "no data" instead of "no points" and quietly inflate every mean.
    /// Deltas can also be negative (a forced discard against an empty deck costs a VP), so the running
    /// total is not monotonic and must be summed rather than tracked as a maximum.
    ///
    /// Team travels with each faction so a consumer can roll AXIS and ALLIES up without hardcoding
    /// who is on which side.
    /// </summary>
    private void EmitRoundScores(GameResult result)
    {
        Dictionary<string, object> factions = new();
        List<string> textRows = new();

        foreach (FactionResult faction in result.Factions)
        {
            Dictionary<int, int> deltaByRound = new();
            foreach (RoundScore scored in faction.PerRound)
                deltaByRound[scored.Round] = scored.Points;

            List<int> deltas = new();
            List<int> totals = new();
            int running = 0;
            for (int round = 1; round <= result.FinalRound; round++)
            {
                int delta = deltaByRound.GetValueOrDefault(round, 0);
                running += delta;
                deltas.Add(delta);
                totals.Add(running);
            }

            factions[faction.Faction.ToString()] = new Dictionary<string, object>
            {
                ["team"] = faction.Team.ToString(),
                ["deltas"] = deltas,
                ["totals"] = totals,
            };

            textRows.Add($"  {faction.Faction,-16} {string.Join(" ", totals)}");
        }

        _renderer.Emit(new CliEvent("round_scores")
            .Set("seed", GameRandom.Seed)
            .Set("decision_seed", _decisionSeed)
            .Set("final_round", result.FinalRound)
            .Set("factions", factions)
            .Text($"ROUNDS  cumulative VP by round 1..{result.FinalRound}\n"
                  + string.Join("\n", textRows)));
    }

    /// <summary>
    /// What happened to every card this game: whether it was ever drawn, how often it was played, and
    /// how often it was activated. The input to asking which cards move the final score.
    ///
    /// Read off CardState at game end rather than accumulated from events. PlayedInTurn and
    /// ActivatedInTurns are already per-card lists of the turns involved and they survive the whole
    /// game, so the tally is a read, not a subscription — nothing to keep in step and nothing to leak.
    ///
    /// "Drawn" is derived rather than tracked: a card still sitting in DeckCardIds at the end never
    /// left the deck, and anything else (hand, discard, played, status, response) did. That avoids
    /// hooking DrawCardsChangeEvent for a fact the final piles already state.
    ///
    /// Cards that were never drawn and never played are omitted. In a 124-card pool that is most of
    /// the tail in a short game, and a consumer can recover absence anyway — it knows the game count,
    /// so "not listed" is "not involved". Emitting them would cost several MB across a large batch to
    /// say nothing.
    ///
    /// NOT the opening hand. That is fixed by `seed` alone, so it is one fact per board rather than
    /// per game, and capturing it here would be too late regardless: the bot answers synchronously for
    /// bot_yield_every prompts before the frame loop regains control, by which point several turns of
    /// draws and discards have already churned the hand.
    /// </summary>
    private void EmitCardStats()
    {
        List<object> cards = new();

        foreach (CardState card in CardState.All.Values)
        {
            int played = card.PlayedInTurn.Count;
            int activated = card.ActivatedInTurns.Count;
            bool drawn = !DeckState.ForFaction(card.Faction).DeckCardIds.Contains(card.Id);

            if (!drawn && played == 0 && activated == 0) continue;

            cards.Add(new Dictionary<string, object>
            {
                ["name"] = card.CardName,
                ["faction"] = card.Faction.ToString(),
                ["type"] = card.CardData?.CardType.ToString() ?? "UNKNOWN",
                ["drawn"] = drawn,
                ["played"] = played,
                ["activated"] = activated,
            });
        }

        _renderer.Emit(new CliEvent("card_stats")
            .Set("seed", GameRandom.Seed)
            .Set("decision_seed", _decisionSeed)
            .Set("cards", cards)
            .Text($"CARDS  {cards.Count} card(s) drawn or played"));
    }

    /// <summary>
    /// EventBus is a process-wide static that outlives every scene, and a Godot signal runs all its
    /// handlers through one multicast delegate — so a stale handler that throws aborts the emission
    /// for everyone behind it. Unsubscribing is not optional bookkeeping.
    /// </summary>
    public void Dispose()
    {
        if (!_subscribed) return;
        _subscribed = false;
        EventBus.Instance.GameEnded -= OnGameEnded;
    }
}
