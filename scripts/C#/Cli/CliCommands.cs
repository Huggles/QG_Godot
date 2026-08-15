using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Parses and dispatches terminal commands. Three tiers, deliberately distinct:
///
///  1. ANSWER  (play / select / pass / choose / discard / order)
///     Fills the pending InputRequest. The in-rules path — this is how a real game advances.
///
///  2. INJECT  (api ...)
///     Pushes a ChangeEvent through CardPlayPool.DoChangeEvent with IsTrigger = true, so other
///     factions get their block and after-reaction windows exactly as they would against a card's
///     step. Same mechanism scenario mutators use (StepMutatorExtensions.Do).
///
///  3. FORCE   (force ...)
///     The same events applied directly with IsTrigger = false. No reactions. Setup/teardown only.
///
/// Mixing 2 and 3 up is silent: an injected event that should have been blockable simply never
/// offers the window. Hence separate verbs rather than a flag.
/// </summary>
public sealed class CliCommands
{
	private readonly CliRenderer _out;
	private readonly CliInputProvider _input;
	private readonly CliSession _session;

	public CliCommands(CliRenderer renderer, CliInputProvider input, CliSession session)
	{
		_out = renderer;
		_input = input;
		_session = session;
	}

	/// <summary>Commands that answer the open prompt, and so must wait for one to exist.</summary>
	private static readonly HashSet<string> AnswerVerbs = new()
	{
		// `prompt` is here rather than with the inspection verbs so that in a script it waits for the
		// next prompt and prints it, instead of racing ahead and reporting "none open".
		"play", "select", "choose", "a", "answer", "discard", "order", "pass", "skip", "prompt",
	};

	/// <summary>Commands that read or mutate game state, and so must wait for a running game.</summary>
	private static readonly HashSet<string> GameVerbs = new()
	{
		"state", "score", "hash", "board", "country", "hand", "deck", "cards", "log",
		"api", "force", "continue", "assert",
	};

	public enum Readiness { Run, WaitForPrompt, WaitForGame }

	/// <summary>Injected effects still working through the reaction pipeline.</summary>
	private int _inFlight;

	/// <summary>
	/// Whether <paramref name="line"/> can run right now.
	///
	/// This gate is what makes a piped script work at all. Commands arrive on stdin far faster than
	/// the game reaches the prompts they answer — a whole script lands in the first frame — so
	/// without it every answer would be dispatched against a game that has not started and be lost.
	/// The caller leaves a not-yet-runnable line queued and retries next frame, which turns the
	/// command stream into "answer prompts as they arrive" for free.
	/// </summary>
	public Readiness CanRun(string line)
	{
		string verb = Verb(line);
		if (verb == null) return Readiness.Run;                      // blank/comment: consume it
		if (AnswerVerbs.Contains(verb)) return _input.HasOpenPrompt
			? Readiness.Run : Readiness.WaitForPrompt;
		// An assertion ABOUT the open prompt has to run while the prompt is open, so it waits on the
		// prompt rather than on the game. The GameVerbs gate below would deadlock it: a reaction
		// window opened by an injected effect is raised while that effect is still in flight, which
		// is precisely the moment these subjects describe.
		if (verb == "assert" && IsPromptSubject(line)) return _input.HasOpenPrompt
			? Readiness.Run : Readiness.WaitForPrompt;
		// Wait for the game to actually settle, not merely for the queue to look idle. See
		// CliSession.IsSettled: straight after an answer the queue is briefly idle while the answer's
		// continuation has yet to enqueue anything, and reading state there silently yields
		// pre-answer values.
		if (GameVerbs.Contains(verb)) return CliStateView.Ready
											&& _inFlight == 0
											&& (ChangeEventQueue.Instance?.IsIdle ?? true)
											&& _session.IsSettled
			? Readiness.Run : Readiness.WaitForGame;
		return Readiness.Run;                                        // json / help / quit / unknown
	}

	/// <summary>Subjects of <c>assert</c> that read the open prompt rather than game state.</summary>
	private static readonly HashSet<string> PromptSubjects = new() { "promptoptions", "promptfaction" };

	private static bool IsPromptSubject(string line)
	{
		int comment = line.IndexOf('#');
		if (comment >= 0) line = line.Substring(0, comment);
		string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		return parts.Length > 1 && PromptSubjects.Contains(parts[1].ToLowerInvariant());
	}

	private static string Verb(string line)
	{
		if (line == null) return null;
		int comment = line.IndexOf('#');
		if (comment >= 0) line = line.Substring(0, comment);
		line = line.Trim();
		if (line.Length == 0) return null;
		return line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
	}

	public void Dispatch(string line)
	{
		if (line == null) return;
		int comment = line.IndexOf('#');
		if (comment >= 0) line = line.Substring(0, comment);
		line = line.Trim();
		if (line.Length == 0) return;

		List<string> parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
		string verb = parts[0].ToLowerInvariant();
		List<string> args = parts.Skip(1).ToList();

		try
		{
			Run(verb, args);
		}
		catch (Exception e)
		{
			// A bad command must never take the game down — report and keep the prompt live.
			_out.Error($"{verb}: {e.Message}");
		}
	}

	private void Run(string verb, List<string> args)
	{
		switch (verb)
		{
			// ── Answering a prompt ───────────────────────────────────────────
			case "play":
			case "select":
			case "choose":
			case "a":
			case "answer":   Answer(args); return;
			case "discard":
			case "order":    Answer(args); return;
			case "pass":
			case "skip":     Pass(args); return;
			case "prompt":
				if (!_input.Reprompt()) _out.Error("no prompt is open");
				return;

			// ── Inspection ───────────────────────────────────────────────────
			case "state":    Guarded(() => _out.Write(CliStateView.Summary())); return;
			case "score":    Guarded(() => _out.Write(CliStateView.Score())); return;
			case "hash":     Guarded(() => _out.Write(CliStateView.Hash())); return;
			case "board":    Guarded(() => _out.Write(CliStateView.Board(ParseFactionOrNull(args.FirstOrDefault())))); return;
			case "country":  Country(args); return;
			case "hand":     Cards(args, "hand"); return;
			case "deck":     Cards(args, "deck"); return;
			case "cards":    Cards(args, args.Count > 1 ? args[1] : "activatable"); return;
			case "log":      Guarded(() => _out.Write(CliStateView.Log(args.Count > 0 && int.TryParse(args[0], out int n) ? n : 15))); return;

			// ── Effect injection ─────────────────────────────────────────────
			case "api":      Inject(args, isTrigger: true); return;
			case "force":    Inject(args, isTrigger: false); return;

			// ── Test ─────────────────────────────────────────────────────────
			case "assert":   Assert(args); return;

			// ── Control ──────────────────────────────────────────────────────
			case "auto":
			case "run":      Auto(args); return;
			case "json":     _out.JsonMode = args.FirstOrDefault() != "off"; _out.Ok($"json {(_out.JsonMode ? "on" : "off")}"); return;
			case "continue": ErrorReporter.RequestResume(); _out.Ok("requested loop resume"); return;
			case "quit":
			case "exit":     _session.Quit(args.Count > 0 && int.TryParse(args[0], out int code) ? code : 0); return;
			case "help":     Help(); return;

			default:         _out.Error($"unknown command '{verb}' — try `help`"); return;
		}
	}

	// ── Answering ────────────────────────────────────────────────────────────

	/// <summary>
	/// Accepts 1-based prompt indices (the primary form), `id=N,N` for raw game ids, or a name.
	/// Indices are primary because they are unambiguous and mean a caller never has to know that a
	/// SelectFaction answer is a faction enum value living in ResponseCardIds.
	/// </summary>
	private void Answer(List<string> args)
	{
		if (args.Count == 0) { _out.Error("expected an option index, `id=N`, or a name"); return; }

		string joined = string.Join(" ", args);

		if (joined.StartsWith("id=", StringComparison.OrdinalIgnoreCase))
		{
			List<int> ids = ParseIntList(joined.Substring(3));
			Report(ids == null ? "could not parse id list" : _input.AnswerById(ids));
			return;
		}

		List<int> indices = ParseIntList(joined);
		Report(indices != null ? _input.AnswerByIndex(indices) : _input.AnswerByName(joined));
	}

	/// <summary>
	/// `pass` / `skip`, with an optional scope for a reaction prompt: `pass turnstep` and
	/// `pass round` are the two scoped skip buttons. Bare `pass` is the plain Skip.
	/// </summary>
	private void Pass(List<string> args)
	{
		string scopeArg = args.FirstOrDefault();
		if (scopeArg == null) { Report(_input.Pass()); return; }

		ReactionSkipScope scope = scopeArg.ToLowerInvariant() switch
		{
			"turnstep" or "turn_step" or "step" => ReactionSkipScope.TURN_STEP,
			"round"                             => ReactionSkipScope.ROUND,
			_                                   => ReactionSkipScope.NONE,
		};

		if (scope == ReactionSkipScope.NONE)
		{
			_out.Error($"unknown pass scope '{scopeArg}' (expected turnstep or round)");
			return;
		}

		Report(_input.Pass(scope));
	}

	private static List<int> ParseIntList(string text)
	{
		List<int> values = new();
		foreach (string token in text.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
		{
			if (!int.TryParse(token, out int value)) return null;
			values.Add(value);
		}
		return values.Count == 0 ? null : values;
	}

	private void Report(string error)
	{
		if (error != null) _out.Error(error);
	}

	// ── Effect injection ─────────────────────────────────────────────────────

	/// <summary>
	/// Build a ChangeEvent from named args and push it through the reaction pipeline (or apply it
	/// directly). Every ChangeEvent constructor takes primitives, so this is a short explicit table
	/// rather than reflection — which also means the error messages can name the expected args.
	/// </summary>
	private void Inject(List<string> args, bool isTrigger)
	{
		if (!CliStateView.Ready) { _out.Error("game is not running yet"); return; }
		if (args.Count == 0) { _out.Error("expected an effect name — try `help`"); return; }

		string effect = args[0].ToLowerInvariant();
		Dictionary<string, string> named = ParseNamed(args.Skip(1));

		ChangeEvent changeEvent = effect switch
		{
			"deployunit"  => new DeployUnitChangeEvent(
								 RequireFaction(named, "faction"),
								 RequireCountry(named, "country"),
								 ParseEnum(named.GetValueOrDefault("deploy"), DeployType.RECRUIT)),
			"removeunit"  => new RemoveUnitChangeEvent(
								 RequireFaction(named, "faction"),
								 RequireInt(named, "unit"),
								 ParseEnum(named.GetValueOrDefault("reason"), UnitRemovalReason.BATTLE)),
			"battle"      => new BattleCountryChangeEvent(
								 RequireFaction(named, "faction"),
								 RequireCountry(named, "country")),
			"battleunit"  => new BattleUnitChangeEvent(
								 RequireFaction(named, "faction"),
								 RequireInt(named, "unit")),
			"drawcards"   => new DrawCardsChangeEvent(
								 RequireFaction(named, "faction"),
								 RequireFaction(named, "target", fallbackKey: "faction"),
								 RequireInt(named, "n")),
			"playcard"    => new PlayCardChangeEvent(RequireCard(named, "card")),
			"recyclecard" => new RecycleCardChangeEvent(
								 RequireFaction(named, "faction"),
								 RequireFaction(named, "target", fallbackKey: "faction"),
								 RequireCard(named, "card"),
								 ParseEnum(named.GetValueOrDefault("destination"), RecycleDestination.ShuffleIntoDeck)),
			_             => null,
		};

		if (changeEvent == null)
		{
			_out.Error($"unknown effect '{effect}'. Known: deployUnit, removeUnit, battle, battleUnit, " +
					   "drawCards, playCard, recycleCard");
			return;
		}

		changeEvent.IsTrigger = isTrigger;

		// Reactions need a CardPlayRound to register against. StepMutatorRunner has exactly this
		// problem and solves it the same way — create one if absent, and afterwards ClearPool()
		// rather than Finish(), because Finish() emits CardPlayPoolFinished and would advance the
		// turn loop a second time.
		// Counted so the readiness gate holds back the next state command until this has settled.
		// Without it a script's `api deployUnit` / `board` pair reads the board in the same frame the
		// effect was dispatched, before any of it has applied.
		_inFlight++;
		Guard.FireAndForget(async () =>
		{
			bool createdRound = CardPlayRound.Current == null;
			if (createdRound) GameFlow.Instance.CardPlayRounds.Add(CardPlayRound.StartNew());
			try
			{
				if (isTrigger) await CardPlayPool.DoChangeEvent(changeEvent);
				else           await changeEvent.Apply();
			}
			finally
			{
				if (createdRound) CardPlayRound.Current?.ClearPool();
				_inFlight--;
			}
		}, $"cli:{effect}", stallsLoop: false);

		_out.Ok($"{(isTrigger ? "api" : "force")} {effect} dispatched" +
				(isTrigger ? " (reactions will be offered)" : " (no reactions)"));
	}

	private static Dictionary<string, string> ParseNamed(IEnumerable<string> args)
	{
		Dictionary<string, string> named = new(StringComparer.OrdinalIgnoreCase);
		string pendingKey = null;
		foreach (string arg in args)
		{
			if (arg.StartsWith("--"))
			{
				string body = arg.Substring(2);
				int eq = body.IndexOf('=');
				if (eq > 0) { named[body.Substring(0, eq)] = body.Substring(eq + 1); pendingKey = null; }
				else pendingKey = body;
			}
			else if (pendingKey != null) { named[pendingKey] = arg; pendingKey = null; }
		}
		return named;
	}

	private static Faction RequireFaction(Dictionary<string, string> named, string key, string fallbackKey = null)
	{
		string value = named.GetValueOrDefault(key) ?? (fallbackKey != null ? named.GetValueOrDefault(fallbackKey) : null);
		if (value == null) throw new Exception($"missing --{key}");
		if (!Enum.TryParse(value, true, out Faction faction)) throw new Exception($"unknown faction '{value}'");
		return faction;
	}

	private static int RequireCountry(Dictionary<string, string> named, string key)
	{
		string value = named.GetValueOrDefault(key) ?? throw new Exception($"missing --{key}");
		CountryState country = int.TryParse(value, out int id) ? CountryState.ForId(id) : CountryState.ForName(value);
		if (country == null) throw new Exception($"unknown country '{value}'");
		return country.Id;
	}

	private static int RequireCard(Dictionary<string, string> named, string key)
	{
		string value = named.GetValueOrDefault(key) ?? throw new Exception($"missing --{key}");
		CardState card = int.TryParse(value, out int id) ? CardState.ForId(id) : CardState.ForName(value);
		if (card == null) throw new Exception($"unknown card '{value}'");
		return card.Id;
	}

	private static int RequireInt(Dictionary<string, string> named, string key)
	{
		string value = named.GetValueOrDefault(key) ?? throw new Exception($"missing --{key}");
		if (!int.TryParse(value, out int parsed)) throw new Exception($"--{key} must be a number");
		return parsed;
	}

	private static T ParseEnum<T>(string value, T fallback) where T : struct, Enum
		=> value != null && Enum.TryParse(value, true, out T parsed) ? parsed : fallback;

	// ── Control / inspection helpers ─────────────────────────────────────────

	private void Auto(List<string> args)
	{
		int count = args.Count > 0 && int.TryParse(args[0], out int n) ? n : 1;
		_input.AutoPassCount = count;
		_out.Ok($"auto-passing the next {count} prompt(s)");
		// Release the prompt already open, or nothing happens until the next one.
		if (_input.HasOpenPrompt && _input.AutoPassCount > 0) { _input.AutoPassCount--; _input.Pass(); }
	}

	/// <summary>
	/// A malformed assertion exits 5 immediately rather than counting as a failure: a typo in a test
	/// is not a regression in the game, and silently "failing" on one would send you hunting a bug
	/// that isn't there.
	/// </summary>
	private void Assert(List<string> args)
	{
		string failure = CliAssert.Evaluate(args, out bool malformed);

		if (malformed) { _out.Error($"assert: {failure}"); _session.Quit(5); return; }
		if (failure == null) { _out.Ok($"assert {string.Join(" ", args)}"); return; }

		_out.Emit(new CliEvent("assert_failed")
			.Set("assertion", string.Join(" ", args))
			.Text($"FAIL {failure}"));
	}

	private void Country(List<string> args)
	{
		if (args.Count == 0) { _out.Error("expected a country name or id"); return; }
		Guarded(() =>
		{
			string text = CliStateView.Country(args[0]);
			if (text == null) _out.Error($"unknown country '{args[0]}'");
			else _out.Write(text);
		});
	}

	private void Cards(List<string> args, string pile)
	{
		if (args.Count == 0) { _out.Error("expected a faction"); return; }
		if (!Enum.TryParse(args[0], true, out Faction faction)) { _out.Error($"unknown faction '{args[0]}'"); return; }
		Guarded(() =>
		{
			string text = CliStateView.Cards(faction, pile);
			if (text == null) _out.Error($"unknown pile '{pile}'");
			else _out.Write(text);
		});
	}

	private static Faction? ParseFactionOrNull(string value)
		=> value != null && Enum.TryParse(value, true, out Faction faction) ? faction : null;

	private void Guarded(Action action)
	{
		if (!CliStateView.Ready) { _out.Error("game is not running yet"); return; }
		action();
	}

	private void Help() => _out.Write(string.Join("\n", new[]
	{
		"ANSWER   play|select|choose|answer <index…>   answer the open prompt by 1-based index",
		"         answer id=<n>[,<n>…]                 answer by raw game id",
		"         select <name>                        answer by country/card/faction name",
		"         pass | skip                          decline (uses this prompt's own pass idiom)",
		"         pass turnstep | pass round           decline, and stop being asked for reactions",
		"         prompt                               re-print the open prompt",
		"INSPECT  state | score | hash | board [faction] | country <name|id>",
		"         hand <faction> | deck <faction> | cards <faction> [pile]",
		"         log [n]",
		"INJECT   api deployUnit --faction F --country C [--deploy RECRUIT]",
		"         api removeUnit --faction F --unit N | api battle --faction F --country C",
		"         api drawCards --faction F --n N | api playCard --card <name|id>",
		"         api recycleCard --faction F --card <name|id> [--destination ShuffleIntoDeck]",
		"         (api = reactions offered;  force = same args, applied directly, no reactions)",
		"TEST     assert <subject> [args] <op> <expected>     op: == != > >= < <= in !in",
		"         subjects: turn round step faction hash errors rngdraws seed",
		"                   promptoptions | promptfaction  (open prompt; -1/NONE when none is open)",
		"                   score <F> | vp <TEAM> | units <country> [faction] | occupant <country>",
		"                   handsize <F> | decksize <F> | discardsize <F> | decktop <F> | deckorder <F>",
		"                   card <name|id>  (-> hand/deck/discard/status/response/nowhere)",
		"                   cardrevealed <name|id>  (-> true/false, Response card face public?)",
		"CONTROL  auto <n> | run <n>   auto-pass n prompts",
		"         json on|off | continue | quit [code] | help",
		"EXIT     0 pass · 1 assertion failed · 2 game error · 5 malformed assertion",
	}));
}
