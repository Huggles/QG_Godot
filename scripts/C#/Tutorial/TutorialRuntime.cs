using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// The turn program a tutorial runs on: a cursor over a <see cref="TutorialScript"/>, drained at
/// GameFlow's four hooks, plus a <see cref="TutorialInputProvider"/> answering the five factions the
/// learner does not control.
///
/// It does NOT supply its own step table. The tutorial runs the real seven-step loop and brackets it,
/// which is what keeps everything downstream honest: ChangeStepChangeEvent still opens each step,
/// VictoryStepHandlerDefault still seeds its VP summary off NewTurnStarted, the HUD still tracks
/// NextStepStarted, and every card condition still reads a TurnStep that means what it says.
///
/// Host-only and single player throughout. There is no client half to this class.
/// </summary>
public sealed class TutorialRuntime : ITurnProgram
{
    /// <summary>
    /// The running tutorial, or null. Read by NetworkApi.SendInputRequest to narrow a constrained
    /// prompt, which is the one place that sees every request type — including the two that bypass
    /// InputRequest.BroadCast.
    /// </summary>
    public static TutorialRuntime Current { get; private set; }

    /// <summary>
    /// The script the next session should run, set while the scenario is parsed and consumed by
    /// <see cref="InstallIfRequested"/>. A static for the same reason GameManager.PendingSave is one:
    /// it has to survive from scenario parsing to session start, and it belongs to neither.
    /// </summary>
    public static string PendingScriptPath { get; set; }

    /// <summary>
    /// Value of <see cref="PendingScriptPath"/> meaning "explicitly no tutorial", as distinct from
    /// null, which means "whatever the scenario says". Set by the CLI's <c>tutorial=none</c>.
    /// </summary>
    public const string NoTutorial = "none";

    private GameFlow _flow;
    private readonly TutorialScript _script;
    private readonly List<TutorialStep> _remaining;
    private bool _finished;

    /// <summary>
    /// Whoever was answering input before the tutorial took over, put back when it stands down.
    /// Restoring beats clearing: under cli=true the displaced provider is CliSession's, and clearing
    /// would fall back to AutoPass — silently ending a scripted run's ability to answer anything.
    /// </summary>
    private IInputProvider _displacedProvider;

    private TutorialRuntime(TutorialScript script)
    {
        _script = script;
        _remaining = script.Steps.ToList();
    }

    // ── Installation ────────────────────────────────────────────────────────────

    /// <summary>
    /// Host-only, called from MultiplayerSession.StartSession before StartGame. A no-op unless the
    /// scenario armed a script.
    /// </summary>
    public static void InstallIfRequested()
    {
        string path = PendingScriptPath;
        PendingScriptPath = null;                 // consumed, so a failure cannot re-arm the next game
        if (string.IsNullOrEmpty(path)) return;

        // The CLI's `tutorial=none` sentinel: run a tutorial scenario as an ordinary game, which is
        // how you tell a rules problem from a script problem. Null cannot express this — it already
        // means "whatever the scenario says".
        if (path == NoTutorial) return;

        // A tutorial is single player by construction: it holds every faction, answers five of them
        // from a script, and blocks the whole game on a CONTINUE button only this peer can press.
        // With peers attached none of that is true, so decline and let the scenario play as an
        // ordinary game rather than half-driving it. Loud, because picking one here is a mistake.
        if (GameFlow.Instance.Multiplayer.GetPeers().Length > 0)
        {
            DebugUtilities.PrintPeerErrorRaw(
                $"Tutorial '{path}' not started: tutorials are single player, and this session has " +
                $"{GameFlow.Instance.Multiplayer.GetPeers().Length} other peer(s). Playing the " +
                "scenario as a normal game instead.");
            return;
        }

        try
        {
            TutorialScript script = TutorialScript.Load(path);
            TutorialRuntime runtime = new(script);

            Current = runtime;
            runtime._displacedProvider = InputServices.Provider;
            InputServices.Override(new TutorialInputProvider(runtime, script));
            GameFlow.Instance.InstallProgram(runtime);

            DebugUtilities.PrintPeer($"Tutorial '{script.Title}' installed from {path}");
        }
        catch (Exception e)
        {
            // A malformed script must not leave a half-installed tutorial behind. Stand everything
            // down and report: the scenario then plays as an ordinary game, which is never a worse
            // outcome than refusing to start at all.
            //
            // Through Reset() rather than Override(null), so a throw before the override was even
            // installed cannot take the CLI's provider with it.
            Reset();
            ErrorReporter.Report(e, "TutorialRuntime.InstallIfRequested");
        }
    }

    /// <summary>
    /// Stand the tutorial down but leave the game running. Called when the script and the board have
    /// come apart — after an error-recovery resume — and when the script says it is done.
    /// </summary>
    public void Abandon(string reason)
    {
        if (_finished) return;
        _finished = true;
        _remaining.Clear();

        InputServices.Override(_displacedProvider);   // every seat goes back to whoever had it
        if (Current == this) Current = null;

        DebugUtilities.PrintPeerErrorRaw($"Tutorial ended: {reason}");
    }

    /// <summary>
    /// Clear tutorial state left over from a previous session. Called from SceneFlow on teardown.
    ///
    /// The override is dropped only when a tutorial installed one. A CLI session owns that same seam
    /// for the life of its process and installs it once at boot, so clearing unconditionally here
    /// would leave a headless run with no way to answer anything.
    /// </summary>
    public static void Reset()
    {
        if (Current != null) InputServices.Override(Current._displacedProvider);
        Current = null;
        PendingScriptPath = null;
    }

    // ── ITurnProgram ────────────────────────────────────────────────────────────

    public void Attach(GameFlow flow) => _flow = flow;

    /// <summary>
    /// A tutorial's own position — the cursor, and which claims have been consumed — is nowhere in
    /// GameFlowSnapshot, and a restore rebuilds the board by replaying the ChangeEvent log, which
    /// carries no PresentationEvents and so would drop every commander message. A tutorial is one or
    /// two rounds; starting it again is the honest answer.
    /// </summary>
    public bool AllowsSaving => false;

    public string SaveBlockedReason
        => "a tutorial cannot be saved — leave and start it again to replay it";

    public async Task OnGameStarted() => await DrainAnchored(null, MutatorTiming.BEFORE);

    public async Task OnTurnStarted(int gameTurn, Faction faction)
        => await DrainAnchored(null, MutatorTiming.BEFORE);

    public async Task BeforeStep(TurnStep step, Faction faction)
        => await DrainAnchored(step, MutatorTiming.BEFORE);

    public async Task AfterStep(TurnStep step, Faction faction)
        => await DrainAnchored(step, MutatorTiming.AFTER);

    public void OnResumeAfterFailure()
    {
        // The failed step's claims are still queued and would be mis-applied to the next matching
        // prompt, so from here the narration would be telling the player to click things that are not
        // there. Guessing at a cursor position is exactly the compounding-error class that recovery
        // exists to prevent; standing down cannot make the board wrong.
        Abandon("the tutorial was interrupted by an error, so guidance has stopped. The game continues.");
    }

    // ── The cursor ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Run every at-anchored instruction that fires at this position, in file order, stopping at the
    /// first that does not match. Prompt-anchored claims are stepped over — they are consumed by the
    /// input seam, not here.
    /// </summary>
    private async Task DrainAnchored(TurnStep? step, MutatorTiming timing)
    {
        if (_finished || _flow == null) return;

        int turn = _flow.GameTurn;
        int round = _flow.Round;
        Faction faction = _flow.CurrentFaction;

        for (int i = 0; i < _remaining.Count; )
        {
            TutorialStep candidate = _remaining[i];

            // A claim waiting for a prompt sits in the list until that prompt arrives; step past it
            // rather than through it, so an `answer` never blocks the message that follows it.
            if (candidate.IsPromptAnchored) { i++; continue; }

            if (!candidate.MatchesAnchor(turn, round, faction, step, timing)) return;

            _remaining.RemoveAt(i);
            await Run(candidate, faction);
            if (_finished) return;
        }
    }

    private async Task Run(TutorialStep step, Faction faction)
    {
        switch (step.Type)
        {
            case TutorialStepType.Message:
                // Logged here rather than left to the animation. Headless, GameMessage.EnqueueAnimations
                // never constructs the animation at all, so the animation's own log line would never
                // run — and then a scripted run could not show that a message fired, or where. That
                // trace is the whole authoring feedback loop, so it belongs on the side that always runs.
                DebugUtilities.PrintPeer($"[tutorial] message @ turn {_flow.GameTurn} {_flow.TurnStep}: {Oneline(step.Text)}");

                // A PresentationEvent rather than a direct call on CommanderMessage.Current: it lands
                // at a defined point in the ordered message stream relative to the effects it narrates,
                // and it earns a history row, which is the only way a player can re-read a message.
                await new ShowCommanderMessagePresentationEvent(faction, step.Text, step.Wait).Apply();
                break;

            case TutorialStepType.Highlight:
            case TutorialStepType.ClearHighlight:
                // Deliberately unimplemented. There is no single board-emphasis API in this project —
                // Tag.Clickable / Tag.PreviewTarget / Tag.FocusTarget for the board,
                // FactionHandDisplay.ShowCardEmphasis for the hand — and picking one here would be
                // designing a feature that real tutorial content should specify first.
                DebugUtilities.PrintPeer($"[tutorial] {step.Type} is not implemented yet");
                break;

            case TutorialStepType.End:
                Abandon("the tutorial finished");
                break;
        }
    }

    /// <summary>A message's first line, for a log that stays one line per instruction.</summary>
    private static string Oneline(string text)
    {
        string flat = (text ?? string.Empty).Replace("\n", " ");
        return flat.Length <= 60 ? flat : flat[..57] + "...";
    }

    // ── The input seam ──────────────────────────────────────────────────────────

    /// <summary>
    /// Take the claim for this prompt, or null if the script does not have one.
    ///
    /// Ordered, but forgiving of position: the FIRST unconsumed claim matching this prompt's kind and
    /// faction wins, wherever it sits in the file. Strict head-of-queue matching would desync on the
    /// first reaction window, because the always-ask rule means the exact prompt sequence is not
    /// knowable from the script.
    /// </summary>
    internal TutorialStep TakeClaimFor(InputRequestSpec spec)
    {
        if (_finished) return null;

        for (int i = 0; i < _remaining.Count; i++)
        {
            TutorialStep candidate = _remaining[i];
            if (!candidate.IsPromptAnchored) continue;
            if (!Claims(candidate, spec)) continue;

            _remaining.RemoveAt(i);
            return candidate;
        }

        return null;
    }

    private static bool Claims(TutorialStep step, InputRequestSpec spec)
        => (step.Prompt == "*" || step.Prompt.Equals(spec.Kind, StringComparison.OrdinalIgnoreCase))
        && (step.PromptFaction == Faction.NONE || step.PromptFaction == spec.Faction);

    /// <summary>
    /// Narrow a constrained prompt to what the lesson allows. Called from NetworkApi.SendInputRequest
    /// right after PopulateTargets, which is the single point every request type passes through —
    /// including CardPlayRound.RequestPlay and RequestBlock, which bypass InputRequest.BroadCast.
    ///
    /// Peeks rather than consumes: the claim is taken later, when the provider resolves the request.
    /// </summary>
    /// <exception cref="TutorialScriptException">
    /// The claim names something this prompt does not offer. Deliberately not caught here: see the
    /// exception's own remarks for why a constraint must never fail quietly.
    /// </exception>
    public void ConstrainRequest(InputRequest request)
    {
        if (_finished || request.TargetFaction != _script.LearnerFaction) return;

        InputRequestSpec spec = InputRequestSpec.For(request);
        TutorialStep claim = _remaining.FirstOrDefault(
            s => s.Type == TutorialStepType.Constrain && Claims(s, spec));
        if (claim == null) return;

        if (claim.Cards.Count > 0) NarrowCards(request, spec, claim);
        if (claim.Countries.Count > 0) NarrowCountries(request, spec, claim);
    }

    /// <summary>
    /// Move the disallowed cards out of the selectable set and into the display set, so they render
    /// greyed out rather than vanishing. That is what DisplayCardIds is for, and it is the right
    /// answer for a tutorial: a card the player can see but not use is itself the explanation.
    /// </summary>
    private void NarrowCards(InputRequest request, InputRequestSpec spec, TutorialStep claim)
    {
        List<int> allowed = Resolve(spec, claim.Cards, CliOptionKind.Card);

        List<int> narrowed = (request.TargetCardIds ?? new List<int>()).Intersect(allowed).ToList();
        if (narrowed.Count == 0)
            // Defensive: spec.Options for a card prompt is built FROM TargetCardIds, so anything
            // Resolve matched is already in it. Reachable only if that ever stops being true — and an
            // empty offer is an unanswerable prompt the loop would park on forever, so it must throw
            // rather than be applied.
            throw new TutorialScriptException(
                $"constraining {spec.Kind} for {spec.Faction} to [{string.Join(", ", claim.Cards)}] " +
                $"would leave nothing selectable (offered: {CliOptionMatcher.Describe(spec)})");

        request.DisplayCardIds ??= new List<int>(request.TargetCardIds);
        request.TargetCardIds = narrowed;

        DebugUtilities.PrintPeer(
            $"[tutorial] constrained {spec.Kind} {spec.Faction} to " +
            $"{narrowed.Count} of {request.DisplayCardIds.Count} card(s)");
    }

    private void NarrowCountries(InputRequest request, InputRequestSpec spec, TutorialStep claim)
    {
        List<int> allowed = Resolve(spec, claim.Countries, CliOptionKind.Country);

        List<int> narrowed = (request.TargetCountryIds ?? new List<int>()).Intersect(allowed).ToList();
        if (narrowed.Count == 0)
            // Defensive, for the reason NarrowCards documents: this prompt's options are built from
            // TargetCountryIds, so a resolved name is already in it.
            throw new TutorialScriptException(
                $"constraining {spec.Kind} for {spec.Faction} to [{string.Join(", ", claim.Countries)}] " +
                $"would leave nothing selectable (offered: {CliOptionMatcher.Describe(spec)})");

        request.TargetCountryIds = narrowed;

        DebugUtilities.PrintPeer(
            $"[tutorial] constrained {spec.Kind} {spec.Faction} to country id(s) {string.Join(", ", narrowed)}");
    }

    /// <summary>
    /// Resolve a constrain's selectors to option ids, throwing if any of them names something this
    /// prompt does not offer.
    ///
    /// Throws rather than returning null and carrying on unconstrained. A constraint that quietly
    /// fails to apply is the worst outcome available: the commander keeps telling the player to play
    /// one card while the whole hand stays live, so the lesson is wrong and nothing reports it. A
    /// stall with a named cause is recoverable; a silently wrong lesson is not.
    /// </summary>
    private List<int> Resolve(InputRequestSpec spec, List<string> names, CliOptionKind kind)
    {
        List<int> ids = new();
        foreach (string name in names)
        {
            CliOption option = CliOptionMatcher.Resolve(spec, name, out string error);
            if (option == null)
                throw new TutorialScriptException(
                    $"constraining {spec.Kind} for {spec.Faction}: {error}");

            if (option.Kind != kind) continue;
            ids.Add(option.Id);
        }

        if (ids.Count == 0)
            throw new TutorialScriptException(
                $"constraining {spec.Kind} for {spec.Faction}: [{string.Join(", ", names)}] " +
                $"named no {kind.ToString().ToLowerInvariant()} this prompt offers " +
                $"(offered: {CliOptionMatcher.Describe(spec)})");

        return ids;
    }

    /// <summary>
    /// The script and the game have come apart. Raw, so it survives the CLI's console muting, and —
    /// under the strict policy — a real popup, because once content is authored a desync means the
    /// narration is now lying about the board.
    /// </summary>
    internal void ReportScriptProblem(string message)
    {
        if (_script.OnUnexpectedPrompt == UnexpectedPromptPolicy.Strict)
            ErrorReporter.Report(new Exception($"Tutorial script: {message}"), "TutorialRuntime");
        else
            DebugUtilities.PrintPeerErrorRaw($"Tutorial script: {message}");
    }
}
