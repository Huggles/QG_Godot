using System.Collections.Generic;

/// <summary>
/// Everything a <see cref="IBotRule"/> is allowed to know about one prompt, assembled once and passed
/// to every applicable rule.
///
/// Deliberately a snapshot of QUESTIONS, not of answers: it carries the request, the normalised offer
/// set and the flow context, and nothing about what the bot is going to do. Rules read game state
/// directly through the ordinary static accessors (CountryState.ForId, UnitState, DeckState, GameFlow)
/// — the sim bot runs on the host, so everything GameStateCalculator computes is live and correct
/// here, replicated or not.
/// </summary>
public sealed class BotDecision
{
    /// <summary>
    /// The request as the provider received it. Rules read the context fields off this —
    /// <see cref="InputRequest.PassCostText"/> (what passing costs, and null when it is free),
    /// <see cref="InputRequest.IsReactionWindow"/>, TriggerCardId, TriggerTargetCountryIds/UnitIds.
    ///
    /// Note this is a fresh deserialised copy, not the host's instance — NetworkApi.ReceiveInputRequest
    /// round-trips the DTO before resolving it. So reference identity to anything host-side is not
    /// available even in-process, and anything a rule needs must either be a field on the request or be
    /// read back out of game state by id.
    /// </summary>
    public InputRequest Request { get; }

    /// <summary>
    /// The legality table's view of the prompt: what may be chosen, how many, and how declining is
    /// expressed. Read it; never write to it. Only the engine narrows the option list, and only after
    /// checking that what remains can still answer the prompt.
    /// </summary>
    public InputRequestSpec Spec { get; }

    /// <summary>The faction being asked. Hoisted off the spec because every rule wants it.</summary>
    public Faction Faction { get; }

    /// <summary>
    /// The offer set as the game presented it, BEFORE any veto — the index space every verdict is
    /// written in. Held separately from <see cref="Spec"/>.Options for exactly that reason: the spec's
    /// list is narrowed in place once the engine has ruled, and a verdict index must not shift under a
    /// rule that has already reported.
    /// </summary>
    public IReadOnlyList<CliOption> Options { get; }

    public int Round { get; }
    public TurnStep Step { get; }

    /// <summary>
    /// Scratch space shared by every rule consulted on THIS prompt, for state reads worth computing
    /// once — two rules that both want CountryState.BuildableLand(faction) should not both scan the
    /// map. Discarded when the prompt is answered; never carry anything across prompts in here.
    /// </summary>
    public Dictionary<string, object> Scratch { get; } = new();

    private BotDecision(InputRequest request, InputRequestSpec spec)
    {
        Request = request;
        Spec = spec;
        Faction = spec.Faction;
        Options = spec.Options;
        Round = GameFlow.Instance?.Round ?? 0;
        Step = GameFlow.Instance?.TurnStep ?? default;
    }

    public static BotDecision Build(InputRequestSpec spec, InputRequest request) => new(request, spec);

    /// <summary>
    /// Memoise a per-prompt state read. The key is the caller's business; prefix it with the rule name
    /// unless the value is genuinely shared.
    /// </summary>
    public T Cached<T>(string key, System.Func<T> compute)
    {
        if (Scratch.TryGetValue(key, out object cached)) return (T)cached;
        T value = compute();
        Scratch[key] = value;
        return value;
    }
}
