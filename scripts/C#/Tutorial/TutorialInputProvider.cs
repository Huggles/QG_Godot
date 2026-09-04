using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Answers InputRequests from the tutorial script, handing the learner's own decisions to the real
/// GUI provider underneath.
///
/// Installed with InputServices.Override, which is exactly what CliSession does — the precedent is
/// deliberate. A tutorial is single player: peer 1 holds all six factions, so PlayerFactionRegistry
/// maps every faction to this peer, IsForCurrentPeer is true for every request, and this one provider
/// therefore sees the learner's prompts and all five opponents'. Nothing has to be routed anywhere.
/// </summary>
public sealed class TutorialInputProvider : IInputProvider
{
    private readonly TutorialRuntime _runtime;
    private readonly TutorialScript _script;
    private readonly IInputProvider _human;

    public TutorialInputProvider(TutorialRuntime runtime, TutorialScript script)
    {
        _runtime = runtime;
        _script = script;

        // AutoPass headless: a `constrain`ed prompt for the learner would otherwise park a scripted
        // run forever waiting for a click that cannot come. That is what makes a tutorial runnable as
        // a smoke test rather than only by hand.
        _human = GameContext.IsHeadless ? new AutoPassInputProvider() : new GodotInputProvider();
    }

    public async Task Resolve(InputRequest request)
    {
        InputRequestSpec spec = InputRequestSpec.For(request);
        TutorialStep claim = _runtime.TakeClaimFor(spec);

        // The learner answers for themself. A `constrain` claim has already narrowed the offer in
        // NetworkApi.SendInputRequest, so by here the prompt simply is what they are allowed to do.
        if (request.TargetFaction == _script.LearnerFaction)
        {
            await _human.Resolve(request);
            return;
        }

        if (claim == null)
        {
            // The always-ask reaction rule opens empty windows constantly — a faction holding a
            // face-down Response is prompted on every qualifying event, and the window must open even
            // when nothing applies. Passing those silently is what a human would do, and without this
            // carve-out the unexpected-prompt policy below would fire dozens of times a turn.
            if (request.IsReactionWindow && spec.Options.Count == 0)
            {
                spec.ApplyPass(request);
                return;
            }

            await HandleUnexpected(spec, request);
            return;
        }

        if (claim.Pass)
        {
            spec.ApplyPass(request);
            return;
        }

        List<CliOption> chosen = Select(spec, claim, out string error);
        if (chosen == null)
        {
            _runtime.ReportScriptProblem(
                $"the script answers {spec.Kind} for {spec.Faction} with something it cannot offer — {error}");
            spec.ApplyPass(request);
            return;
        }

        spec.Apply(request, chosen);
        DebugUtilities.PrintPeer(
            $"[tutorial] {spec.Kind} {spec.Faction} <- {string.Join(", ", chosen.Select(c => c.Label))}");
    }

    /// <summary>Resolve a claim's selectors against this prompt's own options.</summary>
    private static List<CliOption> Select(InputRequestSpec spec, TutorialStep claim, out string error)
    {
        error = null;
        List<CliOption> chosen = new();

        foreach (string name in claim.Cards.Concat(claim.Countries)
                     .Concat(claim.Option == null ? Enumerable.Empty<string>() : new[] { claim.Option }))
        {
            // The first of however many copies the prompt offers. ResolveAll rather than Resolve for
            // the reason it documents: "play Build Army" out of a hand holding three of them is one
            // answer, not an ambiguity, and any copy plays the same.
            List<CliOption> options = CliOptionMatcher.ResolveAll(spec, name, out error);
            if (options == null) return null;
            chosen.Add(options[0]);
        }

        if (chosen.Count == 0)
        {
            error = "it names nothing to choose";
            return null;
        }
        if (chosen.Count < spec.MinSelections || chosen.Count > spec.MaxSelections)
        {
            error = $"it names {chosen.Count} option(s), but this prompt takes " +
                    $"{spec.MinSelections}-{spec.MaxSelections}";
            return null;
        }

        return chosen;
    }

    /// <summary>
    /// A prompt the script does not claim. Which of these is right depends entirely on how finished
    /// the script is, so it is the script's own call — see TutorialScriptData.OnUnexpectedPrompt.
    /// </summary>
    private async Task HandleUnexpected(InputRequestSpec spec, InputRequest request)
    {
        string what = $"{spec.Kind} for {spec.Faction}";

        switch (_script.OnUnexpectedPrompt)
        {
            case UnexpectedPromptPolicy.Strict:
                _runtime.ReportScriptProblem($"nothing in the script answers {what}");
                spec.ApplyPass(request);
                return;

            case UnexpectedPromptPolicy.Pass:
                DebugUtilities.PrintPeerErrorRaw($"[tutorial] passing unscripted {what}");
                spec.ApplyPass(request);
                return;

            default:
                // Lenient: let the player answer it. A half-written script stays playable, which is
                // what you want while authoring; flip to strict to find the gaps.
                //
                // A plain log, not PrintPeerErrorRaw: under lenient this is the DESIGNED behaviour for
                // every prompt the script does not care about, and routing it through GD.PushError
                // attaches a C# backtrace to each one — which buried a headless run's real output in
                // half a megabyte of stack traces. Strict is the mode that shouts.
                DebugUtilities.PrintPeer($"[tutorial] unscripted {what} — handing it to the player");
                await _human.Resolve(request);
                return;
        }
    }
}
