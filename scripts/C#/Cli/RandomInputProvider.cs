using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Answers every <see cref="InputRequest"/> by picking uniformly at random from the options the
/// request itself offers. The driver behind <c>sim=true</c>: it plays a whole game with nobody
/// watching, so a run can be repeated thousands of times to gather balance statistics.
///
/// What is LEGAL lives entirely in <see cref="InputRequestSpec"/> — what may be chosen, how many, and
/// how this particular request expresses "I decline". That table already serves the CLI and the
/// tutorial, so a bot built on it cannot drift out of the rules or answer into the wrong Response*
/// bucket, and a 15th request subclass costs nothing here.
///
/// What is WISE lives in <see cref="BotRuleEngine"/> and its rules, and must never move into that
/// table. The CLI is a scripted human and has to be offered exactly what the game offers; the moment
/// a preference of this bot's is folded into InputRequestSpec, a .qgc script and the game disagree
/// about what is answerable.
///
/// This class keeps only the two decisions the rules are not allowed to make: how MANY options to
/// take, and whether the end-of-turn discard is worth taking at all (<see cref="IsOptionalCost"/> —
/// a statement about what the prompt is, not about which option is better). Everything else is a
/// rule, and every random draw is <see cref="Draw"/>.
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
    /// The rules that veto and rank options; see <see cref="BotRuleEngine"/>. Never null — an empty
    /// rule set is an engine that abstains on every prompt, which is uniform random play.
    ///
    /// Built HERE rather than passed in fully formed because the engine needs this provider's
    /// <see cref="Draw"/> for its suppression rolls, and routing them through the same counted stream
    /// is what keeps <see cref="Draws"/> the whole truth about the decision RNG.
    /// </summary>
    private readonly BotRuleEngine _engine;

    /// <inheritdoc cref="_engine"/>
    public BotRuleEngine Engine => _engine;

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

    /// <summary>
    /// Prompts that were passed — by the roll, for want of any legal option, or because every legal
    /// option was hollow and passing was free.
    /// </summary>
    public int Passed { get; private set; }

    /// <summary>
    /// Draws taken from <see cref="_rng"/> this run, reported on <c>game_result</c> beside
    /// <see cref="Answered"/>.
    ///
    /// Worth carrying because the decision stream is the one thing about a sim run that is supposed to
    /// be a pure function of <c>decision_seed</c>, and when a refactor breaks that the symptom is a
    /// whole game diverging with no clue where. Two runs of the same seed pair that disagree on this
    /// number disagree about how many CHOICES were made, which localises the change to a prompt count
    /// instead of a 400-line transcript diff.
    ///
    /// Every draw goes through <see cref="Draw()"/> / <see cref="Draw(int,int)"/> rather than being
    /// counted at its call site, so an _rng use added later cannot forget to count itself.
    /// </summary>
    public int Draws { get; private set; }

    /// <inheritdoc cref="Draws"/>
    private double Draw()
    {
        Draws++;
        return _rng.NextDouble();
    }

    /// <inheritdoc cref="Draws"/>
    private int Draw(int minInclusive, int maxExclusive)
    {
        Draws++;
        return _rng.Next(minInclusive, maxExclusive);
    }

    public RandomInputProvider(int decisionSeed, double passChance, double discardChance,
                               List<IBotRule> rules, Dictionary<string, BotRuleConfig> ruleConfig,
                               double tierWidth, int yieldEvery, CliRenderer trace)
    {
        _rng = new Random(decisionSeed);
        _passChance = passChance;
        _discardChance = discardChance;
        _yieldEvery = yieldEvery;
        _trace = trace;
        _engine = new BotRuleEngine(rules, ruleConfig, Draw, tierWidth);
    }


    public async Task Resolve(InputRequest request)
    {
        InputRequestSpec spec = InputRequestSpec.For(request);
        Answered++;

        BotDecision decision = BotDecision.Build(spec, request);
        BotAdvice advice = _engine.Consult(decision);

        // Narrowing happens BEFORE the pass decision, not after: whether anything worth doing is on
        // offer is an INPUT to that decision. An option set the rules emptied is how "everything I
        // could play here is pointless" reaches ShouldPass as the empty set it already knows to pass
        // on — and the engine has already refused to empty it where that would leave the prompt
        // unanswerable.
        List<ScoredOption> scored = _engine.Survivors(decision, advice);
        if (advice.AnyVetoes)
        {
            spec.Options = new List<CliOption>(scored.Count);
            foreach (ScoredOption option in scored) spec.Options.Add(option.Option);
        }

        if (ShouldPass(spec, request, advice))
        {
            spec.ApplyPass(request);
            Passed++;
            Trace(spec, null, advice);
        }
        else
        {
            List<CliOption> chosen = Choose(spec, scored, advice);
            spec.Apply(request, chosen);
            Trace(spec, chosen, advice);
        }

        if (_yieldEvery > 0 && Answered % _yieldEvery == 0)
        await Task.Yield();
    }

    /// <summary>
    /// One line per decision. Reports the counts alongside the pick, because "passed" covers three
    /// different things — declined a real choice, had none to make, or had none left once the rules had
    /// spoken — and only the counts tell them apart.
    ///
    /// `offered` is the set the bot actually chose from and `vetoed` is what the rules removed, so the
    /// two together reconstruct what the game put on the table. `hollow` is kept as its own field, even
    /// though it is now just no_hollow's share of `vetoed`, so a script written against the pre-engine
    /// trace still reads.
    /// </summary>
    private void Trace(InputRequestSpec spec, List<CliOption> chosen, BotAdvice advice)
    {
        if (_trace == null) return;

        int vetoed = advice.VetoCount;

        string picked = chosen == null
            ? (spec.Options.Count > 0 ? "pass"
             : vetoed > 0 ? "pass (all vetoed)"
             : "pass (nothing offered)")
            : string.Join(", ", chosen.Select(c => c.Label));

        CliEvent e = new CliEvent("bot_answer")
            .Set("kind", spec.Kind)
            .Set("faction", spec.Faction.ToString())
            .Set("offered", spec.Options.Count)
            .Set("hollow", advice.VetoesBy("no_hollow"))
            .Set("vetoed", vetoed)
            .Set("chosen", chosen?.Select(c => c.Label).ToList());

        // Conditional, so an ordinary line stays readable: on most prompts the rules have nothing to
        // say, and a run is hundreds of prompts long.
        // Who is asking, and what for. Present only on prompts a card step raised, and the card NAME
        // rather than its id because the point of this field is to be read: it is what turns "some
        // SelectCountry prompt is unclassified" into a worklist of cards to annotate.
        if (spec.OriginCardId >= 0)
        {
            e.Set("origin_card", CardState.ForId(spec.OriginCardId)?.CardName ?? $"card#{spec.OriginCardId}");
            e.Set("purpose", spec.OriginPurpose.ToString());
        }

        if (vetoed > 0) e.Set("by", advice.RulesThatVetoed.ToList());
        if (advice.AnyScores) e.Set("scored", advice.NonZeroScores());
        if (advice.Floored) e.Set("floor", true);
        if (advice.PassBy != null) e.Set("pass_by", advice.PassBy);

        string flags = (advice.Floored ? " FLOOR" : "")
                     + (advice.PassBy != null ? $" pass_by={advice.PassBy}" : "");

        string origin = spec.OriginCardId >= 0
            ? $" [{CardState.ForId(spec.OriginCardId)?.CardName ?? "?"}/{spec.OriginPurpose}]"
            : "";

        _trace.Emit(e.Text($"BOT {spec.Kind,-26} {spec.Faction,-15} " +
                           $"offered={spec.Options.Count,-3} vetoed={vetoed,-3} <- {picked}{flags}{origin}"));
    }

    /// <summary>
    /// An empty option list is not a policy decision — an always-ask reaction window offers nothing
    /// and passing is the only legal answer, so that check comes before the roll. ReorderCards is
    /// the other exception: its "pass" echoes the original order, which is a real (and boring)
    /// choice rather than a decline, so let <see cref="Choose"/> shuffle instead.
    ///
    /// The two rule hooks sit AFTER those three structural returns, deliberately: no rule may make a
    /// mandatory prompt pass or a reorder prompt skip, however strongly it feels. A refusal beats a
    /// force because wrongly forcing a pass is silent passivity — the failure this bot exists to avoid
    /// — while wrongly refusing one is a bad play that shows up in the statistics.
    /// </summary>
    private bool ShouldPass(InputRequestSpec spec, InputRequest request, BotAdvice advice)
    {
        if (spec.Options.Count == 0) return true;
        if (!spec.CanPass) return false;
        if (spec.Pass == PassMode.EchoTargets) return false;
        if (advice.RefusesPass) return false;
        if (advice.ForcesPass) return true;
        if (IsOptionalCost(request)) return Draw() >= _discardChance;
        return _passChance > 0 && Draw() < _passChance;
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
    /// Kept here as a private predicate rather than moved into the rule registry, unlike the hollow
    /// filter. It is not a statement about OPTIONS — it is a statement about what the prompt IS — and
    /// it only ever feeds the pass decision, so expressing it as a rule would mean either giving rules
    /// a third power or re-deriving bot_discard as a probabilistic ForcePass, which the no-randomness-
    /// in-rules invariant forbids. Rules can still SCORE this prompt's options, which composes with
    /// bot_discard instead of replacing it, and that is the useful half.
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
    /// Pick a legal number of distinct options, uniformly among the ones the rules ranked highest.
    ///
    /// The lower bound is <c>max(MinSelections, 1)</c>: having already decided not to pass, choosing
    /// zero would be a pass by another route, and for the prompts whose MinSelections is 0 (playing a
    /// card, activating one) that is exactly the passivity this bot exists to avoid.
    ///
    /// HOW MANY to take is drawn first, from the same distribution and the same single draw as before
    /// this method learned about rules — that is the sampler's business, and rules steer only WHICH.
    /// The unscored path below is then byte-for-byte the original: an early return rather than a
    /// single-tier special case, so the equivalence is visible instead of argued.
    /// </summary>
    private List<CliOption> Choose(InputRequestSpec spec, List<ScoredOption> scored, BotAdvice advice)
    {
        int max = Math.Min(spec.MaxSelections, spec.Options.Count);
        int min = Math.Min(Math.Max(spec.MinSelections, 1), max);
        int count = min == max ? min : Draw(min, max + 1);

        if (!advice.AnyScores) return PartialShuffle(new List<CliOption>(spec.Options), count);

        // Fill from the best tier down. A prompt taking more options than the top tier holds spills
        // into the next one, which is the right reading of a multi-select: take everything you prefer,
        // then make up the number at random from what is left.
        List<CliOption> picked = new(count);
        foreach (List<CliOption> tier in _engine.Tiers(scored))
        {
            if (picked.Count >= count) break;
            picked.AddRange(PartialShuffle(tier, Math.Min(count - picked.Count, tier.Count)));
        }
        return picked;
    }

    /// <summary>
    /// Fisher-Yates over the list, returning the first <paramref name="count"/>.
    ///
    /// A partial shuffle rather than repeated rejection sampling so that ReorderCards — which takes
    /// every option — gets a genuinely uniform permutation from the same code path as a one-of-N pick.
    /// Mutates the list it is given, so pass a copy of anything you still need in its original order.
    /// </summary>
    private List<CliOption> PartialShuffle(List<CliOption> pool, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int j = Draw(i, pool.Count);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        return pool.Take(count).ToList();
    }
}
