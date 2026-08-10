using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// The CLI front end. Autoloaded (so it survives MainScene → Game.tscn → VictoryScreen) and inert
/// unless <c>cli=true</c>.
///
/// Owns the transport, drains its inbox on the main thread in _Process, dispatches commands, and
/// mirrors the game's EventBus signals to the terminal.
/// </summary>
public partial class CliSession : Node
{
    public static CliSession Instance { get; private set; }

    private ICliTransport _transport;
    private CliRenderer _renderer;
    private CliInputProvider _input;
    private CliCommands _commands;
    private bool _subscribed;

    public CliRenderer Renderer => _renderer;
    public CliInputProvider Input => _input;

    public override void _Ready()
    {
        if (!GameContext.IsCli) { SetProcess(false); return; }

        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

        _transport = new StdioTransport();
        _transport.Start();

        _renderer = new CliRenderer(_transport, CliArgs.GetBool("json"));
        _input = new CliInputProvider(_renderer);
        _commands = new CliCommands(_renderer, _input, this);

        // Replace the bootstrap auto-pass provider now that there is somewhere to ask.
        InputServices.Override(_input);

        // The popup is suppressed headless, so without this a rules violation or a stalled loop would
        // be completely invisible — the CLI would just look like it ignored the command.
        ErrorReporter.ErrorIngested += OnError;
        ErrorsSeen = 0;
        CliAssert.Reset();

        _renderer.Emit(new CliEvent("ready")
            .Set("scenario", GameManager.PendingScenarioPath)
            .Text($"QG CLI ready — scenario {GameManager.PendingScenarioPath}\nType `help` for commands."));
    }

    public override void _Process(double delta)
    {
        if (!GameContext.IsCli) return;

        SubscribeOnce();
        AnnounceStartOnce();

        // Drain the reader thread's queue on the main thread. Everything downstream — command
        // handlers, prompt answers, ChangeEvent application — runs here, same as the rest of the game.
        //
        // A line that is not runnable yet (an answer with no prompt open, an inspection before the
        // game exists) is held in _held rather than dropped, and retried next frame. That single
        // rule is what lets a whole script be piped in at once: stdin delivers it in the first frame,
        // and it then drips out in step with the prompts the game actually reaches.
        while (true)
        {
            if (_held == null && !_transport.TryRead(out _held)) break;

            CliCommands.Readiness readiness = _commands.CanRun(_held);
            if (readiness != CliCommands.Readiness.Run) break;   // retry this same line next frame

            string line = _held;
            _held = null;
            _commands.Dispatch(line);
        }

        // EOF on stdin, nothing held back, no prompt outstanding: the script is done.
        //
        // Gated on the game having actually started. Without that, an empty or misdirected stdin
        // (a broken pipe, a shell that handed the process /dev/null) makes the session quit during
        // boot and report PASS with zero assertions — a silent green that hides a completely
        // unexecuted test. Exit 5 says "the script never ran", which is a different problem from
        // "the game misbehaved".
        if (!_transport.IsOpen && _held == null && !_input.HasOpenPrompt)
        {
            if (!_announced) { _renderer.Error("stdin ended before the game started — no commands ran"); Quit(5); }
            else Quit(0);
        }
    }

    private string _held;
    private bool _announced;

    /// <summary>
    /// Report the scenario once the game exists. Not in _Ready: this autoload starts before
    /// MainScene runs CliBootstrap, so PendingScenarioPath is still the default there.
    /// </summary>
    private void AnnounceStartOnce()
    {
        if (_announced || !CliStateView.Ready) return;
        _announced = true;
        _renderer.Emit(new CliEvent("game_started")
            .Set("scenario", GameManager.PendingScenarioPath)
            .Text($"GAME  scenario {GameManager.PendingScenarioPath}"));
    }

    /// <summary>
    /// Subscribe to the game's event stream once MultiplayerSession exists. Deferred rather than done
    /// in _Ready because this autoload is up long before the session scene is.
    /// </summary>
    private void SubscribeOnce()
    {
        if (_subscribed || MultiplayerSession.Instance == null) return;
        _subscribed = true;

        EventBus.Instance.NewTurnStarted += OnNewTurn;
        EventBus.Instance.GameChangeEventAfter += OnChangeEventAfter;

        // Deliberately NOT subscribing to NextStepStarted. GameFlow's TurnStepCounter setter emits it
        // *after* Guard.FireAndForget has already started the step handler, so it lands after that
        // step's own work and reads as out-of-order in a linear log. ChangeStepChangeEvent is the
        // authoritative, correctly-ordered step boundary, so the banner is derived from it below.
    }

    private void OnNewTurn(int turn)
    {
        GameFlow flow = GameFlow.Instance;
        _renderer.Emit(new CliEvent("turn")
            .Set("turn", turn).Set("round", flow.Round)
            .Set("faction", flow.CurrentFaction.ToString())
            .Set("team", flow.CurrentFactionTeam.ToString())
            .Text($"\n=== TURN {turn}  ROUND {flow.Round}  {flow.CurrentFaction} ({flow.CurrentFactionTeam})"));
    }

    /// <summary>
    /// One line per applied message. Hooked on GameChangeEventAfter rather than RecordApplied
    /// (which is protected): both ChangeEvent and PresentationEvent emit it immediately after
    /// recording, so the last element of GameMessages is always the one just applied. That also picks
    /// up ShowActionLabelPresentationEvent, which carries a card step's guidance text — the narrative
    /// context a terminal player needs next to a bare option list.
    /// </summary>
    private void OnChangeEventAfter(string changeEventName)
    {
        List<GameMessage> messages = MultiplayerSession.Instance?.GameState?.GameMessages;
        GameMessage last = messages != null && messages.Count > 0 ? messages[^1] : null;
        string summary = last?.SummaryText() ?? changeEventName;

        // A step change is a boundary, not an event — give it a banner instead of a bullet.
        if (last is ChangeStepChangeEvent)
        {
            _renderer.Emit(new CliEvent("step")
                .Set("step", GameFlow.Instance.TurnStep.ToString())
                .Text($"--- STEP {GameFlow.Instance.TurnStep}"));
            return;
        }

        _renderer.Emit(new CliEvent("event")
            .Set("name", changeEventName).Set("summary", summary)
            .Text($"    · {changeEventName}  {summary}"));
    }

    /// <summary>Non-Soft errors seen this run. Drives the exit code for a scripted run.</summary>
    public int ErrorsSeen { get; private set; }

    private void OnError(GameError error)
    {
        if (error.Severity != ErrorSeverity.Soft) ErrorsSeen++;

        _renderer.Emit(new CliEvent("game_error")
            .Set("severity", error.Severity.ToString())
            .Set("message", error.Message)
            .Set("context", error.Context)
            .Text($"!!! {error.Severity} {error.Message}\n    {error.Context}"));
    }

    private bool _quitting;

    public void Quit(int exitCode)
    {
        // Guarded: GetTree().Quit is deferred to the end of the frame, so _Process runs again and the
        // EOF check would call this a second time.
        if (_quitting) return;
        _quitting = true;

        // A clean-looking run that quietly failed an assertion or hit a rules error must not exit 0 —
        // that is the whole point of a scripted run. An explicit non-zero code from `quit N` wins.
        if (exitCode == 0)
        {
            if (CliAssert.Failed > 0) exitCode = 1;
            else if (ErrorsSeen > 0)  exitCode = 2;
        }

        string verdict = exitCode == 0 ? "PASS" : "FAIL";
        _renderer?.Emit(new CliEvent("exit")
            .Set("code", exitCode)
            .Set("asserts", CliAssert.Evaluated)
            .Set("assert_failures", CliAssert.Failed)
            .Set("errors", ErrorsSeen)
            .Set("seed", GameRandom.Seed)
            .Text($"{verdict}  {CliAssert.Evaluated} assert(s), {CliAssert.Failed} failed, " +
                  $"{ErrorsSeen} error(s), seed {GameRandom.Seed}  ->  exit {exitCode}"));

        _transport?.Stop();
        GetTree().Quit(exitCode);
    }
}
