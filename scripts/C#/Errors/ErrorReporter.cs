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
    /// Depth of in-flight <c>ChangeEvent.ApplyChange</c> calls. Non-zero means state may be
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

    private const int MaxQueuedErrors = 20;

    private ErrorPopup _popup;
    private bool _reporting;                                   // re-entrancy guard

    /// <summary>
    /// TurnStepCounter at the moment the newest error was ingested. Continue compares against it so
    /// it cannot advance a loop that already moved on by itself — which happens for a failure that
    /// self-recovered (tier 1) or for a remote peer's error the host was never blocked by.
    /// </summary>
    private int? _counterAtReport;

    /// <summary>Severity of the newest error, so Continue can refuse to advance for a Soft one.</summary>
    private ErrorSeverity _newestSeverity = ErrorSeverity.Recoverable;

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
    private readonly List<GameError> _queue = new();
    private readonly Dictionary<string, GameError> _seen = new();

    /// <summary>Errors reported this session, newest last. Capped at <see cref="MaxQueuedErrors"/>.</summary>
    public IReadOnlyList<GameError> Queue => _queue;

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
        Instance.CallDeferred(MethodName.Ingest, error.ToJson(), false);
    }

    /// <summary>Forget the reported errors so the next failure starts a fresh popup queue.</summary>
    public void ClearQueue()
    {
        _queue.Clear();
        _seen.Clear();
    }

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

        // A Soft error already recovered in place, and the loop never stopped — advancing here would
        // silently skip a whole turn step.
        if (Instance._newestSeverity == ErrorSeverity.Soft)
        {
            DebugUtilities.PrintPeer("ErrorReporter: error self-recovered, nothing to resume");
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

    /// <summary>
    /// Mark <paramref name="e"/> as having escaped <c>ChangeEvent.ApplyChange</c> after the state was
    /// mutated but before clients were told. Called from ApplyChange itself, because the counters that
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
                                       ErrorSeverity? forcedSeverity = null)
    {
        if (e == null) return;
        if (IsBenign(e)) return;

        ErrorReporter reporter = Instance;

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
        reporter.CallDeferred(MethodName.Ingest, error.ToJson(), allowBroadcast);
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
    private void Ingest(string errorJson, bool allowBroadcast)
    {
        if (_reporting) return;
        _reporting = true;
        try
        {
            GameError error = GameError.FromJson(errorJson);
            if (error == null) return;

            // Dedupe: a fault inside a loop must not spawn hundreds of popups.
            if (_seen.TryGetValue(error.DedupeKey, out GameError existing))
            {
                existing.Occurrences++;
                _popup?.Refresh();
                return;
            }
            _seen[error.DedupeKey] = error;

            // Soft = the game already recovered on its own (CardStep abandoned the failed step, the
            // card and turn step carried on). Nothing is waiting on the player, so interrupting them
            // is just noise — the full trace is already in the console either way.
            //
            // Recoverable and Unrecoverable both still show: "Recoverable" does NOT mean the game
            // recovered by itself, it means the loop is STALLED and Continue is the only thing that
            // restarts it. Suppressing that popup would be a silent freeze.
            if (error.Severity == ErrorSeverity.Soft && !ShowRecoveredErrors)
            {
                _suppressedCount++;
                DebugUtilities.PrintPeer(
                    $"ErrorReporter: recovered from {error.ExceptionType} in {error.Context} " +
                    "(no popup — see the trace above; pass show_recovered_errors=true to surface these)");
                if (allowBroadcast && !IsShuttingDown) Propagate(error);
                return;
            }

            if (_queue.Count >= MaxQueuedErrors)
            {
                SafeLog($"ErrorReporter: queue full ({MaxQueuedErrors}), dropping {error.ExceptionType}");
                return;
            }

            _queue.Add(error);
            _counterAtReport = TryReadTurnStepCounter();
            _newestSeverity = error.Severity;

            if (allowBroadcast && !IsShuttingDown)
                Propagate(error);

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
        ClearQueue();
        RequestResume();
    }

    private void ShowPopup(GameError error)
    {
        try
        {
            if (_popup == null || !IsInstanceValid(_popup))
            {
                _popup = new ErrorPopup();
                AddChild(_popup);
            }
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
