using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Answers every <see cref="InputRequest"/> by picking uniformly at random from the options the
/// request itself offers. The driver behind <c>sim=true</c>: it plays a whole game with nobody
/// watching, so a run can be repeated thousands of times to gather balance statistics.
///
/// All of the per-request-type knowledge lives in <see cref="InputRequestSpec"/> — what may be
/// chosen, how many, and how this particular request expresses "I decline". That table already
/// serves the CLI and the tutorial, so a bot built on it cannot drift out of the rules or answer
/// into the wrong Response* bucket, and a 15th request subclass costs nothing here.
///
/// Two properties are load-bearing and worth stating outright:
///
///  1. It NEVER touches <see cref="GameRandom"/>. The decision stream is a private Random seeded
///     from <c>decision_seed</c>, so <c>seed=42 decision_seed=1</c> and <c>seed=42 decision_seed=2</c>
///     deal the identical opening board and then diverge purely on choices. Drawing decisions from
///     GameRandom would advance the shared stream and silently change the deck order the run was
///     supposed to hold fixed — which would make the whole "one seed, many runs" premise false.
///     Keep this file free of any GameRandom reference.
///
///  2. <see cref="Resolve"/> returns a COMPLETED task by default. Headless durations are already 0
///     (GameSettings.GetDuration), so answering inline keeps the turn loop running inside the
///     current frame instead of parking it on a continuation. See <see cref="_yieldEvery"/> for the
///     one reason that is not unconditional.
/// </summary>
public sealed class RandomInputProvider : IInputProvider
{
    private readonly Random _rng;
    private readonly double _passChance;

    /// <summary>
    /// Probability of taking up an OPTIONAL discard (<see cref="IsOptionalCost"/>) rather than
    /// declining it. Default 0 — see that method for why declining is the right default.
    /// </summary>
    private readonly double _discardChance;

    /// <summary>
    /// Where a per-prompt trace goes under <c>verbose=true</c>, and null otherwise.
    ///
    /// Worth the field: the failure this catches is a bot that looks busy but is quietly passing on
    /// everything, which the result line alone cannot distinguish from a game in which there was
    /// nothing available to do. Silent by default — a full game is hundreds of prompts.
    /// </summary>
    private readonly CliRenderer _trace;

    /// <summary>
    /// Yield to the Godot synchronisation context every N prompts, unwinding the call stack.
    ///
    /// Needed because answering inline means a long stretch of the turn loop — step handler, card
    /// steps, ChangeEvent applies, the next prompt — runs as one synchronous chain with nothing to
    /// break it up, and a 120-turn game has no natural bottom. Yielding costs a frame and buys back
    /// stack depth. 0 disables it.
    /// </summary>
    private readonly int _yieldEvery;

    /// <summary>Prompts answered this run. Reported alongside the result as a sanity figure.</summary>
    public int Answered { get; private set; }

    /// <summary>Prompts that were passed, either by the roll or for want of any legal option.</summary>
    public int Passed { get; private set; }

    public RandomInputProvider(int decisionSeed, double passChance, double discardChance,
                               int yieldEvery, CliRenderer trace)
    {
        _rng = new Random(decisionSeed);
        _passChance = passChance;
        _discardChance = discardChance;
        _yieldEvery = yieldEvery;
        _trace = trace;
    }

    public async Task Resolve(InputRequest request)
    {
        InputRequestSpec spec = InputRequestSpec.For(request);
        Answered++;

        if (ShouldPass(spec, request))
        {
            spec.ApplyPass(request);
            Passed++;
            Trace(spec, null);
        }
        else
        {
            List<CliOption> chosen = Choose(spec);
            spec.Apply(request, chosen);
            Trace(spec, chosen);
        }

        if (_yieldEvery > 0 && Answered % _yieldEvery == 0)
        await Task.Yield();
    }

    /// <summary>
    /// One line per decision. Reports the size of the option set alongside the pick, because "passed"
    /// covers two very different things — declined a real choice, or had none to make — and only the
    /// offered count tells them apart.
    /// </summary>
    private void Trace(InputRequestSpec spec, List<CliOption> chosen)
    {
        if (_trace == null) return;

        string picked = chosen == null
            ? (spec.Options.Count == 0 ? "pass (nothing offered)" : "pass")
            : string.Join(", ", chosen.Select(c => c.Label));

        _trace.Emit(new CliEvent("bot_answer")
            .Set("kind", spec.Kind)
            .Set("faction", spec.Faction.ToString())
            .Set("offered", spec.Options.Count)
            .Set("chosen", chosen?.Select(c => c.Label).ToList())
            .Text($"BOT {spec.Kind,-26} {spec.Faction,-15} offered={spec.Options.Count,-3} <- {picked}"));
    }

    /// <summary>
    /// An empty option list is not a policy decision — an always-ask reaction window offers nothing
    /// and passing is the only legal answer, so that check comes before the roll. ReorderCards is
    /// the other exception: its "pass" echoes the original order, which is a real (and boring)
    /// choice rather than a decline, so let <see cref="Choose"/> shuffle instead.
    /// </summary>
    private bool ShouldPass(InputRequestSpec spec, InputRequest request)
    {
        if (spec.Options.Count == 0) return true;
        if (!spec.CanPass) return false;
        if (spec.Pass == PassMode.EchoTargets) return false;
        if (IsOptionalCost(request)) return _rng.NextDouble() >= _discardChance;
        return _passChance > 0 && _rng.NextDouble() < _passChance;
    }

    /// <summary>
    /// The end-of-turn discard: an offer to throw cards away rather than to do something with them.
    /// Its `MinSelections` is 0 because zero is the ordinary answer.
    ///
    /// It needs its own rule because "never pass when there is something to choose" is a statement
    /// about taking ACTIONS, and this is not an action — it is a cost. Treating it as an opportunity
    /// makes the bot discard a random slice of its hand every single turn, and the hand never
    /// recovers: measured over a full 20-round game that left factions with nothing playable on
    /// roughly half their turns, suppressing card plays far more than any rules interaction did. A
    /// statistics run built on that would be measuring the bot, not the game.
    ///
    /// Deliberately NOT <see cref="InputRequest.CardsRequestHandler"/>, which shares this row in
    /// InputRequestSpec and inherits its "Select cards to discard" title but is not a discard at all:
    /// EventFlexibleResources uses it to pick a card out of the discard pile to PLAY. Declining that
    /// is passing up a free card — and, as of this writing, also crashes the card (it indexes
    /// ResponseCardIds[0] without checking for an empty response).
    ///
    /// The mandatory discards are unaffected — <see cref="InputRequest.ForceDiscardHandCardsRequestHandler"/>
    /// reports <see cref="PassMode.NotAllowed"/> and never reaches here.
    /// </summary>
    private static bool IsOptionalCost(InputRequest request)
        => request is InputRequest.HandCardsDiscardRequestHandler;

    /// <summary>
    /// Pick a legal number of distinct options at random.
    ///
    /// The lower bound is <c>max(MinSelections, 1)</c>: having already decided not to pass, choosing
    /// zero would be a pass by another route, and for the prompts whose MinSelections is 0 (playing a
    /// card, activating one) that is exactly the passivity this bot exists to avoid.
    /// </summary>
    private List<CliOption> Choose(InputRequestSpec spec)
    {
        int max = Math.Min(spec.MaxSelections, spec.Options.Count);
        int min = Math.Min(Math.Max(spec.MinSelections, 1), max);
        int count = min == max ? min : _rng.Next(min, max + 1);

        // Fisher-Yates over a copy, taking the first `count`. A partial shuffle rather than repeated
        // rejection sampling so that ReorderCards — which takes every option — gets a genuinely
        // uniform permutation from the same code path as a one-of-N pick.
        List<CliOption> pool = new(spec.Options);
        for (int i = 0; i < count; i++)
        {
            int j = _rng.Next(i, pool.Count);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        return pool.Take(count).ToList();
    }
}
