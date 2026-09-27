using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// One legal answer to a step's prompt and the ChangeEvent it would produce. <see cref="Target"/> is
/// null for a step that asks nothing (<see cref="Choose.Fixed(Func{ChangeEvent})"/>).
/// </summary>
public readonly record struct StepOption(TargetRef? Target, ChangeEvent Event);

/// <summary>
/// A step's body split into its two halves: what the player may choose, and a PURE factory from one
/// choice to the ChangeEvent it causes. The step prompts in between. <see cref="Outcomes"/> skips the
/// prompt and runs the factory on every option — that is how a step's effect is seen before it runs.
///
/// Both halves may take the PREVIOUS step's outcome instead of reading a field the previous step set:
/// on the real path it is what that step actually did, in a projection it is the hypothetical one.
/// Null means "not known" — dereference it with <c>.Value</c> and <see cref="Outcomes"/> reports
/// unknown rather than a guess. Card state that follows a choice goes in <see cref="OnChosen"/>.
/// </summary>
public abstract class StepChoice
{
    private Action<TargetRef?> _onChosen;
    private Func<Task> _beforePrompt;

    /// <summary>Runs on the real path only, after the answer and before the event is built.</summary>
    public StepChoice OnChosen(Action<TargetRef?> onChosen)
    {
        _onChosen = onChosen;
        return this;
    }

    /// <summary>Runs on the real path only, before the prompt — for presentation pacing.</summary>
    public StepChoice BeforePrompt(Func<Task> beforePrompt)
    {
        _beforePrompt = beforePrompt;
        return this;
    }

    /// <summary>The legal answers, in the order the prompt offers them.</summary>
    public abstract List<TargetRef> Options(StepOption? previous);

    protected abstract Task<TargetRef> Prompt(Faction faction, List<TargetRef> options);

    protected abstract ChangeEvent Outcome(TargetRef target, StepOption? previous);

    /// <summary>The real path: prompt, record the choice, build its event. A skip throws StepSkippedException, as before.</summary>
    internal virtual async Task<StepOption> Run(Faction faction, StepOption? previous)
    {
        if (_beforePrompt != null) await _beforePrompt();
        TargetRef chosen = await Prompt(faction, Options(previous));
        _onChosen?.Invoke(chosen);
        return new StepOption(chosen, Outcome(chosen, previous));
    }

    /// <summary>
    /// Every option with the event it would produce. Nothing is prompted, applied or numbered. The
    /// events are native Godot objects: free the ones you do not apply with <see cref="Release"/>.
    /// Null when a factory throws — it may rely on context only the real run has (Rasputitsa's
    /// trigger), or on a <paramref name="previous"/> that is not known.
    /// </summary>
    public IReadOnlyList<StepOption> Outcomes(StepOption? previous)
    {
        List<StepOption> outcomes = new();
        try
        {
            AddOutcomes(outcomes, previous);
            return outcomes;
        }
        catch (Exception)
        {
            Release(outcomes);
            return null;
        }
    }

    protected virtual void AddOutcomes(List<StepOption> into, StepOption? previous)
    {
        foreach (TargetRef target in Options(previous)) into.Add(new StepOption(target, Outcome(target, previous)));
    }

    protected void NotifyChosen(TargetRef? chosen) => _onChosen?.Invoke(chosen);

    protected Task WaitBeforePrompt() => _beforePrompt?.Invoke() ?? Task.CompletedTask;

    /// <summary>Free the events of outcomes that were only inspected. GameMessage is a GodotObject, so the GC will not.</summary>
    public static void Release(IEnumerable<StepOption> outcomes)
    {
        if (outcomes == null) return;
        foreach (StepOption outcome in outcomes)
            if (outcome.Event != null && Godot.GodotObject.IsInstanceValid(outcome.Event)) outcome.Event.Free();
    }
}

/// <summary>
/// The choice factories a card passes to a <see cref="ResultStep"/> or <see cref="RequirementStep"/>.
/// Each wraps exactly the InputRequest the free-form bodies used, so a converted step prompts the same.
/// Every factory has a form whose lambdas also take the previous step's outcome.
/// </summary>
public static class Choose
{
    public static StepChoice CountryFrom(Func<List<int>> countryIds, Func<int, ChangeEvent> outcome)
        => new CountryChoice(_ => countryIds(), (id, _) => outcome(id));

    public static StepChoice CountryFrom(Func<StepOption?, List<int>> countryIds, Func<int, StepOption?, ChangeEvent> outcome)
        => new CountryChoice(countryIds, outcome);

    public static StepChoice UnitFrom(Func<List<int>> unitIds, Func<int, ChangeEvent> outcome, bool allowSkip = true)
        => new UnitChoice(_ => unitIds(), (id, _) => outcome(id), allowSkip);

    public static StepChoice UnitFrom(Func<StepOption?, List<int>> unitIds, Func<int, StepOption?, ChangeEvent> outcome, bool allowSkip = true)
        => new UnitChoice(unitIds, outcome, allowSkip);

    public static StepChoice FactionFrom(Func<List<Faction>> factions, Func<Faction, ChangeEvent> outcome)
        => new FactionChoice(_ => factions(), (f, _) => outcome(f));

    public static StepChoice FactionFrom(Func<StepOption?, List<Faction>> factions, Func<Faction, StepOption?, ChangeEvent> outcome)
        => new FactionChoice(factions, outcome);

    public static StepChoice BattleTargetFrom(Func<List<BattleTarget>> targets, Func<BattleTarget, ChangeEvent> outcome)
        => new BattleTargetChoice(_ => targets(), (t, _) => outcome(t));

    public static StepChoice BattleTargetFrom(Func<StepOption?, List<BattleTarget>> targets, Func<BattleTarget, StepOption?, ChangeEvent> outcome)
        => new BattleTargetChoice(targets, outcome);

    /// <summary>No prompt: the step has exactly one outcome.</summary>
    public static StepChoice Fixed(Func<ChangeEvent> outcome) => new FixedChoice(_ => outcome());

    /// <inheritdoc cref="Fixed(Func{ChangeEvent})"/>
    public static StepChoice Fixed(Func<StepOption?, ChangeEvent> outcome) => new FixedChoice(outcome);

    private sealed class CountryChoice(Func<StepOption?, List<int>> ids, Func<int, StepOption?, ChangeEvent> outcome) : StepChoice
    {
        public override List<TargetRef> Options(StepOption? previous)
            => ids(previous).Select(id => new TargetRef(TargetKind.Country, id)).ToList();

        protected override async Task<TargetRef> Prompt(Faction faction, List<TargetRef> options)
        {
            InputRequest response = await new InputRequest.SelectCountryRequestHandler(faction, options.Select(o => o.Id).ToList()).BroadCast();
            return new TargetRef(TargetKind.Country, response.ResponseCountryIds[0]);
        }

        protected override ChangeEvent Outcome(TargetRef target, StepOption? previous) => outcome(target.Id, previous);
    }

    private sealed class UnitChoice(Func<StepOption?, List<int>> ids, Func<int, StepOption?, ChangeEvent> outcome, bool allowSkip) : StepChoice
    {
        public override List<TargetRef> Options(StepOption? previous)
            => ids(previous).Select(id => new TargetRef(TargetKind.Unit, id)).ToList();

        protected override async Task<TargetRef> Prompt(Faction faction, List<TargetRef> options)
        {
            InputRequest response = await new InputRequest.SelectUnitRequestHandler(faction, options.Select(o => o.Id).ToList(), allowSkip).BroadCast();
            return new TargetRef(TargetKind.Unit, response.ResponseUnitIds[0]);
        }

        protected override ChangeEvent Outcome(TargetRef target, StepOption? previous) => outcome(target.Id, previous);
    }

    private sealed class FactionChoice(Func<StepOption?, List<Faction>> factions, Func<Faction, StepOption?, ChangeEvent> outcome) : StepChoice
    {
        public override List<TargetRef> Options(StepOption? previous)
            => factions(previous).Select(f => new TargetRef(TargetKind.Faction, (int)f)).ToList();

        // SelectFactionRequestHandler answers in ResponseCardIds — see its Handle().
        protected override async Task<TargetRef> Prompt(Faction faction, List<TargetRef> options)
        {
            InputRequest response = await new InputRequest.SelectFactionRequestHandler(faction, options.Select(o => (Faction)o.Id).ToList()).BroadCast();
            return new TargetRef(TargetKind.Faction, response.ResponseCardIds[0]);
        }

        protected override ChangeEvent Outcome(TargetRef target, StepOption? previous) => outcome((Faction)target.Id, previous);
    }

    private sealed class BattleTargetChoice(Func<StepOption?, List<BattleTarget>> targets, Func<BattleTarget, StepOption?, ChangeEvent> outcome) : StepChoice
    {
        public override List<TargetRef> Options(StepOption? previous) => targets(previous)
            .Where(t => t != null && t.Type != TargetType.NONE)
            .Select(ToRef)
            .ToList();

        protected override async Task<TargetRef> Prompt(Faction faction, List<TargetRef> options)
        {
            InputRequest response = await new InputRequest.SelectBattleTargetRequestHandler(faction, options.Select(ToBattleTarget).ToList()).BroadCast();
            return response.ResponseCountryIds.Count > 0
                ? new TargetRef(TargetKind.Country, response.ResponseCountryIds[0])
                : new TargetRef(TargetKind.Unit, response.ResponseUnitIds[0]);
        }

        protected override ChangeEvent Outcome(TargetRef target, StepOption? previous) => outcome(ToBattleTarget(target), previous);

        private static TargetRef ToRef(BattleTarget t)
            => new(t.Type == TargetType.UNIT ? TargetKind.Unit : TargetKind.Country, t.Id);

        private static BattleTarget ToBattleTarget(TargetRef r)
            => new(r.Id, r.Kind == TargetKind.Unit ? TargetType.UNIT : TargetType.COUNTRY);
    }

    private sealed class FixedChoice(Func<StepOption?, ChangeEvent> outcome) : StepChoice
    {
        public override List<TargetRef> Options(StepOption? previous) => new();

        protected override Task<TargetRef> Prompt(Faction faction, List<TargetRef> options)
            => throw new InvalidOperationException("A fixed step has no prompt");

        protected override ChangeEvent Outcome(TargetRef target, StepOption? previous) => outcome(previous);

        internal override async Task<StepOption> Run(Faction faction, StepOption? previous)
        {
            await WaitBeforePrompt();
            NotifyChosen(null);
            return new StepOption(null, outcome(previous));
        }

        protected override void AddOutcomes(List<StepOption> into, StepOption? previous)
            => into.Add(new StepOption(null, outcome(previous)));
    }
}
