using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The accumulated opinion of every rule consulted on one prompt, and the <see cref="IBotVerdictSink"/>
/// the rules wrote it into.
///
/// One object serves both roles so that consulting N rules allocates one sink rather than N. The engine
/// calls <see cref="BeginRule"/> before handing this to each rule, which is what lets a verdict be
/// attributed to its author — needed for the trace ("vetoed by no_hollow"), for the per-rule counters,
/// and for the <c>floored</c> statistic that catches a rule whose vetoes are always discarded.
///
/// Nothing here decides anything. <see cref="BotRuleEngine"/> reads this and rules on it.
/// </summary>
public sealed class BotAdvice : IBotVerdictSink
{
    private readonly IReadOnlyList<CliOption> _options;
    private readonly bool[] _vetoed;
    private readonly double[] _scores;

    /// <summary>Vetoes and scores written, per rule name. The denominators for the telemetry.</summary>
    private readonly Dictionary<string, int> _vetoesByRule = new();
    private readonly Dictionary<string, int> _scoresByRule = new();

    // The rule currently writing, and the guard rails that apply to it.
    private string _currentRule;
    private double _currentWeight = 1.0;
    private IReadOnlySet<CliOptionKind> _currentOptionKinds;
    private bool _currentWroteAnything;

    /// <summary>Set when any rule wrote a verdict the engine has to act on.</summary>
    public bool AnyVetoes { get; private set; }
    public bool AnyScores { get; private set; }

    public bool ForcesPass { get; private set; }
    public bool RefusesPass { get; private set; }

    /// <summary>The rule behind <see cref="ForcesPass"/> / <see cref="RefusesPass"/>, for the trace.</summary>
    public string PassBy { get; private set; }

    /// <summary>
    /// True when the engine's safety floor discarded this prompt's vetoes because honouring them would
    /// have left the prompt unanswerable. The single most diagnostic field in the whole engine: a rule
    /// whose vetoes are floored as often as they fire looks enabled, reports activity, and changes
    /// nothing at all.
    /// </summary>
    public bool Floored { get; private set; }

    public BotAdvice(IReadOnlyList<CliOption> options)
    {
        _options = options;
        _vetoed = new bool[options.Count];
        _scores = new double[options.Count];
    }

    // ── Engine-facing ────────────────────────────────────────────────────────

    public void BeginRule(IBotRule rule, double weight)
    {
        _currentRule = rule.Name;
        _currentWeight = weight;
        _currentOptionKinds = rule.OptionKinds;
        _currentWroteAnything = false;
    }

    /// <summary>Whether the rule that just ran had anything to say. Feeds its <c>fired</c> counter.</summary>
    public bool EndRule() => _currentWroteAnything;

    public bool IsVetoed(int index) => _vetoed[index];
    public double ScoreAt(int index) => _scores[index];

    public IEnumerable<string> RulesThatVetoed => _vetoesByRule.Keys;
    public int VetoCount => _vetoed.Count(v => v);
    public int VetoesBy(string ruleName) => _vetoesByRule.TryGetValue(ruleName, out int n) ? n : 0;
    public int ScoresBy(string ruleName) => _scoresByRule.TryGetValue(ruleName, out int n) ? n : 0;

    public void MarkFloored() => Floored = true;

    /// <summary>Label → total score, non-zero entries only. The trace's readable form.</summary>
    public Dictionary<string, double> NonZeroScores()
    {
        Dictionary<string, double> result = new();
        for (int i = 0; i < _options.Count; i++)
            if (_scores[i] != 0) result[_options[i].Label] = _scores[i];
        return result;
    }

    // ── Rule-facing (IBotVerdictSink) ────────────────────────────────────────

    public void Veto(int optionIndex, string why = null)
    {
        if (!Writable(optionIndex)) return;
        if (_vetoed[optionIndex]) return;   // a second rule vetoing the same option is not two vetoes
        _vetoed[optionIndex] = true;
        AnyVetoes = true;
        Bump(_vetoesByRule);
    }

    public void Score(int optionIndex, double points)
    {
        if (!Writable(optionIndex)) return;
        if (points == 0) return;
        _scores[optionIndex] += points * _currentWeight;
        AnyScores = true;
        Bump(_scoresByRule);
    }

    public void ForcePass(string why = null)
    {
        ForcesPass = true;
        PassBy ??= _currentRule;
        _currentWroteAnything = true;
    }

    public void RefusePass(string why = null)
    {
        RefusesPass = true;
        // Overwrites, unlike ForcePass: a refusal wins, so the trace should name the rule that won.
        PassBy = _currentRule;
        _currentWroteAnything = true;
    }

    /// <summary>
    /// Reject a write instead of trusting it. Out of range means the rule miscounted the option list,
    /// and the wrong option kind means a rule reached across the one prompt whose options span two
    /// kinds. Both are bugs in the rule, and both are silent unless caught here — so they throw in a
    /// debug build's terms: the engine's try/catch turns this into a reported, disabled rule rather
    /// than a corrupted decision.
    /// </summary>
    private bool Writable(int index)
    {
        if (index < 0 || index >= _options.Count)
            throw new System.ArgumentOutOfRangeException(nameof(index),
                $"bot rule '{_currentRule}' wrote a verdict for option {index} of {_options.Count}");

        if (_currentOptionKinds != null && !_currentOptionKinds.Contains(_options[index].Kind))
            throw new System.InvalidOperationException(
                $"bot rule '{_currentRule}' wrote a verdict for a {_options[index].Kind} option " +
                $"but declares OptionKinds {string.Join("/", _currentOptionKinds)}");

        return true;
    }

    private void Bump(Dictionary<string, int> counter)
    {
        counter[_currentRule] = counter.TryGetValue(_currentRule, out int n) ? n + 1 : 1;
        _currentWroteAnything = true;
    }
}
