using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Single funnel for every unexpected failure. Registered first in [autoload] so it exists before
/// anything else can fail; its UI is built lazily on the first report, so it has no dependency on
/// autoload ordering.
///
/// Responsibilities: filter benign control-flow exceptions, log the FULL trace, show an in-game
/// popup, propagate to other peers (a host-side failure would otherwise leave clients staring at a
/// frozen board), and hold the classification state that decides whether recovery is safe.
/// </summary>
public partial class ErrorReporter : Node
{
    public static ErrorReporter Instance { get; private set; }

    // ── Recovery classification state ────────────────────────────────────────

    /// <summary>
    /// Bumped every time the loop recovers. Continuations belonging to an aborted pipeline compare
    /// against this and unwind via <see cref="AbortedEpochException"/> instead of mutating state
    /// alongside the resumed loop.
    /// </summary>
    public static int GameLoopEpoch { get; private set; } = 0;

    /// <summary>
    /// Depth of in-flight <c>ChangeEvent.Apply</c> calls. Non-zero means state may be
    /// half-mutated, which (combined with <see cref="BroadcastSent"/>) decides severity.
    /// </summary>
    public static int MutationDepth { get; set; } = 0;

    /// <summary>
    /// True once the innermost in-flight ChangeEvent has reached its clients. A throw with
    /// MutationDepth &gt; 0 and this false means the peers are already divergent.
    /// </summary>
    public static bool BroadcastSent { get; set; } = false;

    /// <summary>
    /// Set immediately before every scene change / quit. While set, failures are logged but never
    /// popped up or broadcast — teardown legitimately produces ObjectDisposedException and
    /// NullReferenceException from freed Godot wrappers.
    /// </summary>
    public static bool IsShuttingDown { get; set; } = false;

    // ── Internals ────────────────────────────────────────────────────────────

    /// <summary>How many errors the in-game history keeps. Oldest are evicted past this.</summary>
    private const int HistoryLimit = 10;

    private ErrorPopup _popup;
    private bool _reporting;                                   // re-entrancy guard

    /// <summary>
    /// TurnStepCounter at the moment the newest error was ingested. Continue compares against it so
    /// it cannot advance a loop that already moved on by itself — which happens for a failure that
    /// self-recovered (tier 1) or for a remote peer's error the host was never blocked by.
    /// </summary>
    private int? _counterAtReport;

    /// <summary>
    /// The error that stopped the turn loop and has not been acted on yet, or null when nothing is
    /// waiting. This — not "the newest error" — is what decides whether the popup offers Continue,
    /// which is what makes opening the history by hotkey safe: with no pending stall there is no
    /// Continue button, so browsing cannot skip a turn step.
    /// </summary>
    private GameError _pendingStall;

    /// <summary>True when a failure is waiting on the player's decision.</summary>
    public bool HasPendingStall => _pendingStall != null;

    /// <summary>Severity of the pending stall, used to pick between Continue and a disabled Continue.</summary>
    public ErrorSeverity PendingSeverity => _pendingStall?.Severity ?? ErrorSeverity.Soft;

    /// <summary>
    /// Set while <c>NetworkApi.SendInputRequest</c> is holding a timed-out request open, waiting for the
    /// host to choose Retry or Skip. Distinct from <see cref="_pendingStall"/> on purpose: the loop is
    /// parked by that live await, NOT by a failed step, so Continue must resolve this instead of calling
    /// <see cref="RequestResume"/> — resuming would bump the epoch and advance a step that is already
    /// being held, i.e. the very double-advance this whole change exists to remove.
    /// </summary>
    private TaskCompletionSource<bool> _pendingInputDecision;

    /// <summary>True when a timed-out input request is waiting on the host's Retry / Skip decision.</summary>
    public bool HasPendingInputDecision => _pendingInputDecision != null;

    private int _suppressedCount;

    /// <summary>
    /// How many self-recovered (Soft) failures were logged without a popup this session. Worth
    /// knowing: they are still real bugs, just ones that did not interrupt play.
    /// </summary>
    public int SuppressedCount => _suppressedCount;

    private static bool? _showRecovered;

    /// <summary>
    /// When true, self-recovered failures get a popup too. Off by default so ordinary play is not
    /// interrupted by something the game already handled; turn it on with the
    /// <c>show_recovered_errors=true</c> command-line arg while authoring cards, where these
    /// originate.
    /// </summary>
    public static bool ShowRecoveredErrors
    {
        get
        {
            _showRecovered ??= OS.GetCmdlineUserArgs().Contains("show_recovered_errors=true");
            return _showRecovered.Value;
        }
        set => _showRecovered = value;
    }
    private readonly List<GameError> _history = new();
    private readonly Dictionary<string, GameError> _seen = new();

    /// <summary>
    /// The last <see cref="HistoryLimit"/> errors this session, newest last. Survives dismissal —
    /// closing the popup acknowledges the errors, it does not forget them. Includes self-recovered
    /// (Soft) failures, which never raise a popup, so this is the only place to see them in game.
    /// </summary>
    public IReadOnlyList<GameError> History => _history;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        InstallBackstops();

        // Clear IsShuttingDown once a new scene root is in the tree, so failures in the incoming
        // scene are reported normally again. One hook instead of a SceneReady() call in every scene.
        GetTree().NodeAdded += node =>
        {
            if (node.GetParent() == GetTree().Root)
                IsShuttingDown = false;
        };

        if (!InputMap.HasAction(HistoryAction))
            SafeLog($"ErrorReporter: InputMap action '{HistoryAction}' is missing — the error-history hotkey will not work.");

        InstallTestHooks();
    }

    /// <summary>Command-line driven hooks so automated runs can exercise the hotkey and the ring buffer.</summary>
    private void InstallTestHooks()
    {
        int flood = ErrorInjection.FloodCount;
        if (flood > 0)
            GetTree().CreateTimer(2.0).Timeout += () => ErrorInjection.Flood(flood);

        foreach (double at in ErrorInjection.RepeatErrorAt)
        {
            GetTree().CreateTimer(at).Timeout += () =>
            {
                SafeLog($"[repeat_error_at] reporting the recurring error (t={at}s)");
                ErrorInjection.ReportRepeat();
            };
        }

        foreach (double at in ErrorInjection.AutoOpenHistoryAt)
        {
            GetTree().CreateTimer(at).Timeout += () =>
            {
                SafeLog($"[auto_open_history] firing {HistoryAction} (t={at}s)");
                // A real action event through the normal input pipeline, so this validates the
                // InputMap action name and _UnhandledInput, not just a direct method call.
                Input.ParseInputEvent(new InputEventAction { Action = HistoryAction, Pressed = true });
            };
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
            IsShuttingDown = true;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Report an unexpected failure. Safe to call from any thread and from any state — it filters
    /// benign types, never throws, and never recurses.
    /// </summary>
    /// <param name="context">Where this happened, e.g. "TurnStep SUPPLY" or "CardStep Blitzkrieg #2".</param>
    /// <param name="target">Faction the failing work was being done for, when there is one.</param>
    public static void Report(Exception e, string context = null, Faction? target = null)
        => ReportInternal(e, context, target, allowBroadcast: true);

    /// <summary>
    /// Report a failure that escaped to the turn loop, so the loop is now stalled and the popup must
    /// offer Continue. Called by <see cref="Guard"/> at the loop-driving call sites only.
    ///
    /// Being explicit matters: severity alone is not the signal. A cosmetic animation failure is
    /// "Recoverable" but stalls nothing, and offering Continue for it would advance a turn step that
    /// nothing was waiting on.
    /// </summary>
    public static void ReportLoopStalled(Exception e, string context = null, Faction? target = null)
        => ReportInternal(e, context, target, allowBroadcast: true, stallsLoop: true);

    /// <summary>
    /// Report a failure that has ALREADY been recovered in place, so the popup is informational and
    /// Continue must not advance the turn loop. Used by <c>CardStep.Execute</c>, whose catch abandons
    /// only the failed step while the card and the turn step carry on by themselves.
    /// </summary>
    public static void ReportRecovered(Exception e, string context = null, Faction? target = null)
        => ReportInternal(e, context, target, allowBroadcast: true, forcedSeverity: ErrorSeverity.Soft);

    /// <summary>
    /// Report without touching the network. For menu and teardown paths, where there is often no
    /// peer connected and an <c>Rpc</c> would itself throw.
    /// </summary>
    public static void ReportLocalOnly(Exception e, string context = null, Faction? target = null)
        => ReportInternal(e, context, target, allowBroadcast: false);

    /// <summary>Report a pre-built error arriving from another peer. No further propagation.</summary>
    public static void ReportFromRemote(GameError error)
    {
        if (error == null) return;

        // Log on the receiving peer too. The authoritative loop runs on the host, so most errors a
        // client needs to know about originate elsewhere — without this they appear only in the popup
        // and a client-side log would show nothing at all.
        SafeLog($"(reported by {error.OriginLabel})\n{error.ToPlainText()}");

        if (Instance == null) return;
        Instance.CallDeferred(MethodName.Ingest, error.ToJson(), false, false);
    }

    /// <summary>
    /// Mark everything as seen by the player, WITHOUT forgetting the errors. Called when the popup
    /// closes — the difference between "I have read this" and "this never happened", so the history
    /// stays browsable by hotkey afterwards.
    ///
    /// Deliberately does NOT clear <see cref="_pendingStall"/>: closing the popup does not un-stall
    /// the turn loop. Only actually resuming (or quitting) does. So if you close without choosing,
    /// re-opening still offers Continue — and <c>RequestResume</c> still works, which it would not if
    /// acknowledging had wiped the state it guards on.
    /// </summary>
    public void AcknowledgeAll()
    {
        foreach (GameError error in _history)
            error.Acknowledged = true;
    }

    /// <summary>Drop the pending-stall state on leaving the game, where resuming is moot.</summary>
    public void AbandonPendingStall()
    {
        _pendingStall = null;

        // Dropped, deliberately NOT resolved. Resolving would return "skip" into a step that would then
        // try to apply ChangeEvents against a torn-down scene and a null MultiplayerPeer. Leaving the
        // awaiter pending means no gameplay code runs at all, which is what we want while quitting; the
        // whole GameFlow is about to be freed with it.
        _pendingInputDecision = null;
    }

    /// <summary>Wipe the history entirely. Not on any production path; kept for a deliberate reset.</summary>
    public void ClearHistory()
    {
        _history.Clear();
        _seen.Clear();
        _pendingStall = null;
    }

    /// <summary>
    /// Open the error history, or close it if it is already open. Bound to the
    /// <c>debug_error_history</c> action; see <see cref="_UnhandledInput"/>.
    /// </summary>
    public void ToggleHistory()
    {
        if (GameContext.IsHeadless) return;

        if (_popup != null && IsInstanceValid(_popup) && _popup.Visible)
        {
            DebugUtilities.PrintPeer("ErrorReporter: closing error history");
            _popup.CloseFromHotkey();
            return;
        }

        EnsurePopup();
        _popup?.PresentHistory();
        string span = _history.Count > 0
            ? $" — oldest \"{_history[0].Message}\", newest \"{_history[^1].Message}\""
            : "";
        DebugUtilities.PrintPeer(
            $"ErrorReporter: opened error history ({_history.Count} error(s), " +
            $"{_suppressedCount} self-recovered, loopStalled={HasPendingStall}){span}");
    }

    /// <summary>
    /// The hotkey is handled here rather than through <c>InputManager.KeyClicked</c> for two reasons:
    /// this is an autoload, so it exists in the menu and lobby where InputManager.Current is null;
    /// and ErrorPopup disables InputManager's processing while it is open, so a KeyClicked binding
    /// could open the popup but never close it. (KeyClicked also fires on key release.)
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(HistoryAction))
        {
            ToggleHistory();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>InputMap action that opens the history. Defined in project.godot, bound to F8.</summary>
    public const string HistoryAction = "debug_error_history";

    /// <summary>
    /// Resume the turn loop after a reported failure. Releases every awaiter that the aborted step
    /// left dangling, bumps the epoch so stale continuations become inert, then hands off to
    /// <c>GameFlow.ResumeAfterFailure()</c>. Server only — clients follow via replication.
    /// </summary>
    public static void RequestResume()
    {
        if (Instance == null) return;
        if (Instance.Multiplayer?.MultiplayerPeer != null && !Instance.Multiplayer.IsServer())
            return;

        // Nothing is stalled — either the failure self-recovered, or the popup was opened by hotkey
        // to browse history. Advancing here would silently skip a whole turn step.
        if (Instance._pendingStall == null)
        {
            DebugUtilities.PrintPeer("ErrorReporter: nothing is stalled, nothing to resume");
            return;
        }

        // Likewise if the loop moved on under its own power between the report and the click.
        int? counterNow = Instance.TryReadTurnStepCounter();
        if (Instance._counterAtReport.HasValue && counterNow.HasValue
            && counterNow.Value != Instance._counterAtReport.Value)
        {
            DebugUtilities.PrintPeer(
                $"ErrorReporter: turn loop already advanced ({Instance._counterAtReport} → {counterNow}), " +
                "not resuming");
            return;
        }

        // Order matters: bump the epoch FIRST so anything released below unwinds as stale rather
        // than re-entering the pipeline we are about to restart.
        BumpEpoch();
        MutationDepth = 0;
        BroadcastSent = false;
        Instance._pendingStall = null;
        CancelPendingAwaiters();

        try
        {
            GameFlow.Instance?.ResumeAfterFailure();
        }
        catch (Exception e)
        {
            SafeLog($"ErrorReporter: resume failed: {e}");
        }
    }

    private int? TryReadTurnStepCounter()
    {
        try { return GameFlow.Instance?.TurnStepCounter; }
        catch { return null; }
    }

    /// <summary>
    /// Arm the stall for an error that was reported deeper in the stack and then rethrown, reaching a
    /// loop-driving Guard. The history entry already exists; only the "loop is stopped" fact is new.
    /// Deferred so it runs on the main thread alongside <see cref="Ingest"/>.
    /// </summary>
    private void MarkStalledFromRethrow()
    {
        if (_history.Count == 0) return;

        _pendingStall = _history[^1];
        _pendingStall.Acknowledged = false;
        _counterAtReport = TryReadTurnStepCounter();

        // The popup was raised by the original report; re-render so Continue appears now that the
        // loop is known to be stopped.
        if (_popup != null && IsInstanceValid(_popup) && _popup.Visible)
            _popup.Refresh();
    }

    /// <summary>
    /// Release every awaiter an aborted step can leave dangling. Without this, "Continue" deadlocks
    /// even though the exception was caught, because the loop advances only via signals/TCS that
    /// the failed step never completed.
    /// </summary>
    public static void CancelPendingAwaiters()
    {
        Guard.Try(() => GameFlow.Instance?.CancelCurrentStepHandler(), "cancel:stepHandler");
        Guard.Try(() => NetworkApi.Instance?.CancelPendingInputRequest(), "cancel:inputRequest");
        Guard.Try(() => NetworkApi.Instance?.AbortRemoteInput(), "cancel:remoteInput");
        Guard.Try(() => AnimationQueue.Instance?.CancelAll(), "cancel:animationQueue");
        Guard.Try(() => PresentationModal.Current?.CancelPending(), "cancel:presentationModal");

        // A timed-out request still awaiting Retry/Skip is one more dangling awaiter. Resolve it as
        // Skip: the recovery path is restarting the loop from the step boundary, so re-sending the
        // request would put a prompt back on screen for a step that is being abandoned.
        Guard.Try(() => ResolveInputDecision(retry: false), "cancel:inputDecision");
    }

    // ── Timed-out input: Retry / Skip ────────────────────────────────────────

    /// <summary>
    /// Report that <paramref name="request"/> went unanswered for the whole backstop window, and return
    /// the host's decision: true to re-send the same request, false to give up and let the step continue
    /// as if the player had passed.
    ///
    /// <paramref name="forced"/> when the host ended the window early from the countdown's "time out now"
    /// button, which only changes the wording — the decision it asks for is the same one.
    ///
    /// The caller keeps the step's await alive for as long as this task is pending — that, and not any
    /// stall bookkeeping, is what stops the turn loop from moving on. Reported WITHOUT
    /// <c>stallsLoop</c> for the same reason: arming <see cref="_pendingStall"/> would make the popup's
    /// Continue call <see cref="RequestResume"/>, which advances the step the caller is still holding.
    /// </summary>
    public static Task<bool> ReportInputTimeoutAndAwaitDecision(InputRequest request, bool forced = false)
    {
        ErrorReporter reporter = Instance;

        // No reporter (or no UI to decide with) means there is nobody to ask — fall back to the old
        // behaviour and let the caller skip, rather than parking the loop with no way out.
        if (reporter == null || GameContext.IsHeadless || IsShuttingDown)
            return Task.FromResult(false);

        // Only one input request is ever in flight, so an existing decision means a previous popup was
        // never answered. Release it as Skip rather than losing its awaiter.
        ResolveInputDecision(retry: false);

        TaskCompletionSource<bool> decision = new(TaskCreationOptions.RunContinuationsAsynchronously);
        reporter._pendingInputDecision = decision;

        // Set before reporting: ReportInternal defers to Ingest, which shows the popup, whose button
        // state reads HasPendingInputDecision to decide whether Retry appears.
        //
        // Local only, unlike every other report here. Propagating it would raise a popup on the client
        // too, and ErrorPopup disables that client's InputManager while it is open — so a Retry would
        // arrive at a peer that cannot click the board until it dismisses a popup about a decision it
        // does not own. Its prompt has already been released by AbortRemoteInput, and the countdown
        // reappearing is the explanation it actually needs.
        ReportLocalOnly(BuildInputTimeout(request, forced), "InputTimeout", request?.TargetFaction);

        return decision.Task;
    }

    /// <summary>
    /// The Id is part of the message on purpose: <c>GameError.DedupeKey</c> is built from the message,
    /// and each attempt gets a fresh Id, so a second timeout on the same request type is a distinct
    /// entry that raises its own popup instead of quietly incrementing an occurrence counter.
    /// </summary>
    private static InputTimeoutException BuildInputTimeout(InputRequest request, bool forced = false)
    {
        string bulletin = string.IsNullOrEmpty(request?.TriggerBulletinLabel)
            ? ""
            : $" for Bulletin \"{request.TriggerBulletinLabel}\"";

        string cause = forced
            ? $"You timed out {request?.TargetFaction}'s {request?.GetType().Name}{bulletin} " +
              $"early (Id {request?.Id})."
            : $"{request?.TargetFaction} did not answer {request?.GetType().Name}{bulletin} " +
              $"within {NetworkApi.InputResponseTimeoutMinutes} minutes (Id {request?.Id}).";

        return new InputTimeoutException(
            $"{cause} " +
            "The turn loop is holding here — Retry asks again, Skip continues as if the player passed.");
    }

    /// <summary>
    /// Hand the caller of <see cref="ReportInputTimeoutAndAwaitDecision"/> its answer. Safe to call
    /// when nothing is pending. Called by the popup's Retry / Skip buttons and by the recovery sweep.
    /// </summary>
    public static void ResolveInputDecision(bool retry)
    {
        ErrorReporter reporter = Instance;
        TaskCompletionSource<bool> decision = reporter?._pendingInputDecision;
        if (decision == null) return;

        reporter._pendingInputDecision = null;
        DebugUtilities.PrintPeer($"ErrorReporter: timed-out input resolved as {(retry ? "Retry" : "Skip")}");
        decision.TrySetResult(retry);
    }

    /// <summary>
    /// Bump the epoch. Called by the recovery path so stale continuations become inert.
    /// Returns the new epoch.
    /// </summary>
    public static int BumpEpoch()
    {
        GameLoopEpoch++;
        DebugUtilities.PrintPeer($"ErrorReporter: game-loop epoch → {GameLoopEpoch}");
        return GameLoopEpoch;
    }

    /// <summary>
    /// Throws <see cref="AbortedEpochException"/> if <paramref name="capturedEpoch"/> is stale.
    /// Call at the top of any resumable gameplay continuation.
    /// </summary>
    public static void ThrowIfStaleEpoch(int capturedEpoch)
    {
        if (capturedEpoch != GameLoopEpoch)
            throw new AbortedEpochException(capturedEpoch, GameLoopEpoch);
    }

    // ── Filtering ────────────────────────────────────────────────────────────

    /// <summary>
    /// True for exceptions that are normal control flow or deliberate aborts and must never reach
    /// the player. Note <see cref="StepSkippedException"/> can escape outside CardStep (e.g.
    /// DiscardStepHandlerDefault calls BroadCast() with no catch), so filtering it here rather than
    /// only at the catch sites is what keeps a player pressing Skip from producing a popup.
    /// </summary>
    public static bool IsBenign(Exception e) => e switch
    {
        StepSkippedException => true,
        AbortedEpochException => true,
        OperationCanceledException => true,   // covers TaskCanceledException
        _ => false
    };

    /// <summary>Key used to tag an exception that escaped the mutate-then-broadcast divergence window.</summary>
    private const string DivergedKey = "QG.Diverged";

    /// <summary>Key marking an exception already reported closer to the source, with better context.</summary>
    private const string ReportedKey = "QG.Reported";

    /// <summary>
    /// Mark <paramref name="e"/> as already reported, so a catch further up the stack re-reports
    /// nothing. Used where an inner frame knows more than an outer one — <c>CardStep</c> knows the
    /// card and step id, the turn-step Guard only knows the step.
    /// </summary>
    public static void MarkReported(Exception e)
    {
        if (e == null) return;
        try { e.Data[ReportedKey] = true; }
        catch { /* some exception types have a read-only Data dictionary */ }
    }

    private static bool IsAlreadyReported(Exception e)
    {
        for (Exception current = e; current != null; current = current.InnerException)
        {
            try { if (current.Data.Contains(ReportedKey)) return true; }
            catch { /* ignore and keep walking */ }
        }
        return false;
    }

    /// <summary>
    /// Mark <paramref name="e"/> as having escaped <c>ChangeEvent.Apply</c> after the state was
    /// mutated but before clients were told. Called from Apply itself, because the counters that
    /// identify that window are unwound by its finally block long before the exception reaches the
    /// Guard catch at the top of the turn loop.
    /// </summary>
    public static void MarkDiverged(Exception e)
    {
        if (e == null || IsBenign(e)) return;
        try { e.Data[DivergedKey] = true; }
        catch { /* some exception types have a read-only Data dictionary */ }
    }

    private static bool HasDiverged(Exception e)
    {
        for (Exception current = e; current != null; current = current.InnerException)
        {
            try { if (current.Data.Contains(DivergedKey)) return true; }
            catch { /* ignore and keep walking */ }
        }
        return false;
    }

    private static ErrorSeverity Classify(Exception e)
    {
        // State was mutated but the change never reached the clients, so the peers have already
        // diverged — and the resync repair in NetworkApi.RequestResync is commented out, so there is
        // nothing honest to continue into.
        if (HasDiverged(e))
            return ErrorSeverity.Unrecoverable;

        // A rules/authoring mismatch (GameRuleException) throws before mutating anything.
        return ErrorSeverity.Recoverable;
    }

    // ── Report pipeline ──────────────────────────────────────────────────────

    private static void ReportInternal(Exception e, string context, Faction? target, bool allowBroadcast,
                                       ErrorSeverity? forcedSeverity = null, bool stallsLoop = false)
    {
        if (e == null) return;
        if (IsBenign(e)) return;

        ErrorReporter reporter = Instance;

        // Already reported closer to the source, with richer context (e.g. CardStep knows the card
        // name). Do not duplicate it — but if it has now escaped to the turn loop, still arm the
        // stall so the popup offers Continue.
        if (IsAlreadyReported(e))
        {
            if (stallsLoop && reporter != null)
                reporter.CallDeferred(MethodName.MarkStalledFromRethrow);
            return;
        }

        // Re-entrancy: a failure raised while reporting must not recurse. Log and drop.
        if (reporter != null && reporter._reporting)
        {
            SafeLog($"ErrorReporter: dropped a nested error while reporting: {e}");
            return;
        }

        GameError error;
        try
        {
            error = Build(e, context, target, forcedSeverity);
        }
        catch (Exception buildFailure)
        {
            // Never let the reporter be the thing that takes the game down.
            SafeLog($"ErrorReporter: failed to build error record ({buildFailure.Message}) for: {e}");
            return;
        }

        // Always log immediately and synchronously — even if there is no reporter node yet, or the
        // popup is unavailable, the trace must reach the console.
        SafeLog(error.ToPlainText());

        if (reporter == null) return;

        // Report() can arrive on the finalizer thread (see the UnobservedTaskException backstop),
        // so everything that touches Godot objects is marshalled to the main thread.
        reporter.CallDeferred(MethodName.Ingest, error.ToJson(), allowBroadcast, stallsLoop);
    }

    private static GameError Build(Exception e, string context, Faction? target, ErrorSeverity? forcedSeverity)
    {
        int originPeer = 0;
        string originLabel = "UNKNOWN";
        try
        {
            MultiplayerApi mp = Instance?.Multiplayer;
            originPeer = mp?.MultiplayerPeer != null ? mp.GetUniqueId() : 0;

            // Godot reports a unique id of 1 even with no session, so "peer 1" alone does not mean
            // host — check for actual connected peers before claiming a multiplayer role. Otherwise a
            // failure in the main menu would be labelled HOST, which reads as a session problem.
            bool inSession = mp?.MultiplayerPeer != null
                             && (mp.GetPeers().Length > 0 || originPeer != 1);

            originLabel = GameContext.IsHeadless ? "SERVER"
                : !inSession ? "LOCAL"
                : originPeer == 1 ? "HOST"
                : $"CLIENT {originPeer}";
        }
        catch { /* keep the defaults */ }

        int targetPeer = 0;
        if (target.HasValue)
        {
            try { targetPeer = PlayerFactionRegistry.GetPeerIdForFaction(target.Value); }
            catch { targetPeer = 0; }
        }

        return new GameError
        {
            Message = e.Message ?? "",
            ExceptionType = e.GetType().Name,
            StackTrace = e.ToString(),        // includes inner exceptions and full trace
            Severity = forcedSeverity ?? Classify(e),
            OriginPeerId = originPeer,
            OriginLabel = originLabel,
            TargetFaction = target,
            TargetPeerId = targetPeer,
            Context = BuildContext(context, target),
            Timestamp = SafeTimestamp()
        };
    }

    /// <summary>
    /// Breadcrumb from live game state — the same data DebugOverlay shows. Wrapped in its own
    /// try/catch because these lookups can themselves fail (FactionState.ForEnum can return null,
    /// GameFlow.Instance may not exist yet in the menus): the context builder must never be the
    /// thing that throws.
    /// </summary>
    private static string BuildContext(string context, Faction? target)
    {
        List<string> parts = new();
        if (!string.IsNullOrEmpty(context)) parts.Add(context);

        try
        {
            GameFlow flow = GameFlow.Instance;
            if (flow != null)
            {
                parts.Add($"Turn {flow.GameTurn}");
                parts.Add($"Round {flow.Round}");
                parts.Add($"Step {flow.TurnStep}");
                if (!target.HasValue) parts.Add($"Faction {flow.CurrentFaction}");
            }
        }
        catch { parts.Add("(game state unavailable)"); }

        try
        {
            CardPlayRound round = CardPlayRound.Current;
            if (round?.LastChangeEvent != null)
                parts.Add($"LastEvent {round.LastChangeEvent.ScriptName}");
        }
        catch { /* optional detail */ }

        return parts.Count > 0 ? string.Join(" · ", parts) : "(no context)";
    }

    private static string SafeTimestamp()
    {
        try { return Time.GetDatetimeStringFromSystem(); }
        catch { return "unknown"; }
    }

    /// <summary>Logging that cannot itself throw, for use on the failure path.</summary>
    private static void SafeLog(string message)
    {
        try { DebugUtilities.PrintPeerErrorRaw(message); }
        catch
        {
            try { GD.PrintErr(message); } catch { /* nothing left to try */ }
        }
    }

    // ── Main-thread side ─────────────────────────────────────────────────────

    /// <summary>Main-thread entry point. Deferred target of <see cref="ReportInternal"/>.</summary>
    /// <summary>
    /// Raised for every ingested error. Exists because the popup — the only other way an error
    /// becomes visible — is suppressed when headless, so a scripted or CLI run would otherwise fail
    /// completely silently. Subscribers must not throw.
    /// </summary>
    public static event Action<GameError> ErrorIngested;

    private void Ingest(string errorJson, bool allowBroadcast, bool stallsLoop)
    {
        if (_reporting) return;
        _reporting = true;
        try
        {
            GameError error = GameError.FromJson(errorJson);
            if (error == null) return;

            // Before the dedupe/popup logic: a scripted consumer wants every occurrence, and must
            // still hear about an error that is deduped away from the UI.
            try { ErrorIngested?.Invoke(error); } catch { /* never let a listener break reporting */ }

            bool silent = error.Severity == ErrorSeverity.Soft && !ShowRecoveredErrors;

            // Dedupe: a fault inside a loop must not spawn hundreds of popups. The map lives for the
            // whole session so Occurrences accumulates honestly, which means a recurrence AFTER the
            // player dismissed the popup lands here with the popup hidden — and Refresh() no-ops when
            // hidden. Re-presenting in that case is what stops an acknowledged error from being
            // silently swallowed on every later recurrence.
            if (_seen.TryGetValue(error.DedupeKey, out GameError existing))
            {
                existing.Occurrences++;

                if (_popup != null && IsInstanceValid(_popup) && _popup.Visible)
                {
                    _popup.Refresh();
                }
                else if (existing.Acknowledged && !silent && !GameContext.IsHeadless && !IsShuttingDown)
                {
                    existing.Acknowledged = false;
                    _counterAtReport = TryReadTurnStepCounter();
                    if (stallsLoop) _pendingStall = existing;
                    ShowPopup(existing);
                }
                else if (stallsLoop)
                {
                    // Popup is up showing something else, but the loop is now stalled on this one.
                    _pendingStall = existing;
                    _counterAtReport = TryReadTurnStepCounter();
                }
                return;
            }

            _seen[error.DedupeKey] = error;

            // Ring eviction, not "drop the newest": a session that produces more than HistoryLimit
            // errors must keep the RECENT ones, which are the ones you are debugging.
            _history.Add(error);
            while (_history.Count > HistoryLimit)
            {
                GameError evicted = _history[0];
                _history.RemoveAt(0);
                if (_seen.TryGetValue(evicted.DedupeKey, out GameError mapped) && ReferenceEquals(mapped, evicted))
                    _seen.Remove(evicted.DedupeKey);
            }

            if (allowBroadcast && !IsShuttingDown)
                Propagate(error);

            // Soft = the game already recovered on its own (CardStep abandoned the failed step, the
            // card and turn step carried on). Nothing is waiting on the player, so interrupting them
            // is just noise — but it IS recorded above, so the history is where you go to find what
            // the game quietly papered over.
            //
            // Recoverable and Unrecoverable both still show: "Recoverable" does NOT mean the game
            // recovered by itself, it means the loop is STALLED and Continue is the only thing that
            // restarts it. Suppressing that popup would be a silent freeze.
            if (silent)
            {
                _suppressedCount++;
                error.Acknowledged = true;   // never pending; it is already in the history
                DebugUtilities.PrintPeer(
                    $"ErrorReporter: recovered from {error.ExceptionType} in {error.Context} " +
                    "(no popup — in the error history, or pass show_recovered_errors=true to surface these)");
                return;
            }

            _counterAtReport = TryReadTurnStepCounter();
            if (stallsLoop && error.Severity != ErrorSeverity.Soft)
                _pendingStall = error;

            if (!GameContext.IsHeadless && !IsShuttingDown)
                ShowPopup(error);

            // Test hook: exercises the resume path in a headless run, where there is no popup to click.
            if (ErrorInjection.AutoContinue && error.Severity != ErrorSeverity.Unrecoverable)
            {
                SafeLog("[auto_continue_errors] resuming the turn loop");
                CallDeferred(MethodName.AutoResume);
            }
        }
        catch (Exception ex)
        {
            SafeLog($"ErrorReporter: failure inside Ingest: {ex}");
        }
        finally
        {
            _reporting = false;
        }
    }

    /// <summary>Deferred target for the auto_continue_errors test hook.</summary>
    private void AutoResume()
    {
        // Acknowledge rather than clear, so history survives a headless verification run.
        AcknowledgeAll();
        RequestResume();
    }

    /// <summary>Create the popup on first use. Shared by the reactive and hotkey paths.</summary>
    private void EnsurePopup()
    {
        try
        {
            if (_popup == null || !IsInstanceValid(_popup))
            {
                _popup = new ErrorPopup();
                AddChild(_popup);
            }
        }
        catch (Exception ex)
        {
            SafeLog($"ErrorReporter: could not create popup: {ex}");
        }
    }

    private void ShowPopup(GameError error)
    {
        try
        {
            EnsurePopup();
            if (_popup == null) return;

            _popup.Present(error);
            DebugUtilities.PrintPeer(
                $"ErrorReporter: popup shown (visible={_popup.Visible}, layer={_popup.Layer}, " +
                $"severity={error.Severity})");
        }
        catch (Exception ex)
        {
            SafeLog($"ErrorReporter: could not show popup: {ex}");
        }
    }

    /// <summary>
    /// Send to the other peers. The authoritative loop runs on the host, so without this a
    /// host-side failure leaves every client silently frozen with nothing on screen.
    /// </summary>
    private void Propagate(GameError error)
    {
        try
        {
            if (Multiplayer?.MultiplayerPeer == null || !Multiplayer.HasMultiplayerPeer()) return;
            if (NetworkApi.Instance == null) return;

            string json = error.ToJson();
            if (Multiplayer.IsServer())
            {
                // Authority → every client directly.
                NetworkApi.Instance.Rpc(nameof(NetworkApi.BroadcastError), json);
            }
            else
            {
                // A client's Rpc with AnyPeer only reaches the server, which rebroadcasts.
                NetworkApi.Instance.RpcId(1, nameof(NetworkApi.ReportErrorToServer), json);
            }
            DebugUtilities.PrintPeerFinest($"ErrorReporter: propagated {error.ExceptionType} to peers");
        }
        catch (Exception ex)
        {
            // The failure being reported may *be* the network. Never let propagation throw.
            SafeLog($"ErrorReporter: could not propagate error: {ex.Message}");
        }
    }

    // ── Backstops ────────────────────────────────────────────────────────────

    /// <summary>
    /// Last-resort nets. Neither participates in recovery — <see cref="Guard"/> at each call site
    /// is the primary mechanism.
    /// </summary>
    private void InstallBackstops()
    {
        // Fires on the FINALIZER thread, only when a faulted Task is garbage collected — so it is
        // late, and never fires at all for tasks that stay referenced (which is exactly the hang
        // case). Not dead code even with Guard installed: AnimationQueue's ContinueWith never reads
        // task.Exception, so faulted animations surface here.
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            args.SetObserved();
            Exception e = args.Exception?.GetBaseException();
            if (e == null || IsBenign(e)) return;
            ReportInternal(e, "[UNOBSERVED — context lost]", null, allowBroadcast: false);
        };

        // Cannot prevent termination (IsTerminating will be true), so its only value is a durable
        // log — the popup cannot be shown while the process is dying.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            try
            {
                string text = $"[{SafeTimestamp()}] FATAL: {args.ExceptionObject}";
                SafeLog(text);
                using FileAccess file = FileAccess.Open("user://crash.log", FileAccess.ModeFlags.WriteRead);
                if (file != null)
                {
                    file.SeekEnd();
                    file.StoreLine(text);
                }
            }
            catch { /* dying anyway */ }
        };
    }
}
