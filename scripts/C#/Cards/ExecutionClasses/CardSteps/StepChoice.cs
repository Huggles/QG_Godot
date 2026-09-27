using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// One legal answer to a step's prompt and the ChangeEvent it would produce. <see cref="Target"/> is
/// null for a step that asks nothing (<see cref="Choose.Fixed"/>).
/// </summary>
public readonly record struct StepOption(TargetRef? Target, ChangeEvent Event);

/// <summary>
/// A step's body split into its two halves: what the player may choose, and a PURE factory from one
/// choice to the ChangeEvent it causes. The step prompts in between. <see cref="Outcomes"/> skips the
/// prompt and runs the factory on every option — that is how a step's effect is seen before it runs.
///
/// The factory runs for options nobody chose, so it must not touch state. Card state that follows a
/// choice goes in <see cref="OnChosen"/>, which only runs on the real path.
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
    public abstract List<TargetRef> Options();

    protected abstract Task<TargetRef> Prompt(Faction faction, List<TargetRef> options);

    protected abstract ChangeEvent Outcome(TargetRef target);

    /// <summary>The real path: prompt, record the choice, build its event. A skip throws StepSkippedException, as before.</summary>
    internal virtual async Task<CardStepResult> Run(Faction faction)
    {
        if (_beforePrompt != null) await _beforePrompt();
        TargetRef chosen = await Prompt(faction, Options());
        _onChosen?.Invoke(chosen);
        return Outcome(chosen);
    }

    /// <summary>
    /// Every option with the event it would produce. Nothing is prompted, applied or numbered. The
    /// events are native Godot objects: free the ones you do not apply with <see cref="Release"/>.
    /// Null when a factory throws — it may rely on context only the real run has (Rasputitsa's trigger).
    /// </summary>
    public IReadOnlyList<StepOption> Outcomes()
    {
        List<StepOption> outcomes = new();
        try
        {
            AddOutcomes(outcomes);
            return outcomes;
        }
        catch (Exception)
        {
            Release(outcomes);
            return null;
        }
    }

    protected virtual void AddOutcomes(List<StepOption> into)
    {
        foreach (TargetRef target in Options()) into.Add(new StepOption(target, Outcome(target)));
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
/// </summary>
public static class Choose
{
    public static StepChoice CountryFrom(Func<List<int>> countryIds, Func<int, ChangeEvent> outcome)
        => new CountryChoice(countryIds, outcome);

    public static StepChoice UnitFrom(Func<List<int>> unitIds, Func<int, ChangeEvent> outcome, bool allowSkip = true)
        => new UnitChoice(unitIds, outcome, allowSkip);

    public static StepChoice FactionFrom(Func<List<Faction>> factions, Func<Faction, ChangeEvent> outcome)
        => new FactionChoice(factions, outcome);

    public static StepChoice BattleTargetFrom(Func<List<BattleTarget>> targets, Func<BattleTarget, ChangeEvent> outcome)
        => new BattleTargetChoice(targets, outcome);

    /// <summary>No prompt: the step has exactly one outcome.</summary>
    public static StepChoice Fixed(Func<ChangeEvent> outcome) => new FixedChoice(outcome);

    private sealed class CountryChoice(Func<List<int>> ids, Func<int, ChangeEvent> outcome) : StepChoice
    {
        public override List<TargetRef> Options() => ids().Select(id => new TargetRef(TargetKind.Country, id)).ToList();

        protected override async Task<TargetRef> Prompt(Faction faction, List<TargetRef> options)
        {
            InputRequest response = await new InputRequest.SelectCountryRequestHandler(faction, options.Select(o => o.Id).ToList()).BroadCast();
            return new TargetRef(TargetKind.Country, response.ResponseCountryIds[0]);
        }

        protected override ChangeEvent Outcome(TargetRef target) => outcome(target.Id);
    }

    private sealed class UnitChoice(Func<List<int>> ids, Func<int, ChangeEvent> outcome, bool allowSkip) : StepChoice
    {
        public override List<TargetRef> Options() => ids().Select(id => new TargetRef(TargetKind.Unit, id)).ToList();

        protected override async Task<TargetRef> Prompt(Faction faction, List<TargetRef> options)
        {
            InputRequest response = await new InputRequest.SelectUnitRequestHandler(faction, options.Select(o => o.Id).ToList(), allowSkip).BroadCast();
            return new TargetRef(TargetKind.Unit, response.ResponseUnitIds[0]);
        }

        protected override ChangeEvent Outcome(TargetRef target) => outcome(target.Id);
    }

    private sealed class FactionChoice(Func<List<Faction>> factions, Func<Faction, ChangeEvent> outcome) : StepChoice
    {
        public override List<TargetRef> Options() => factions().Select(f => new TargetRef(TargetKind.Faction, (int)f)).ToList();

        // SelectFactionRequestHandler answers in ResponseCardIds — see its Handle().
        protected override async Task<TargetRef> Prompt(Faction faction, List<TargetRef> options)
        {
            InputRequest response = await new InputRequest.SelectFactionRequestHandler(faction, options.Select(o => (Faction)o.Id).ToList()).BroadCast();
            return new TargetRef(TargetKind.Faction, response.ResponseCardIds[0]);
        }

        protected override ChangeEvent Outcome(TargetRef target) => outcome((Faction)target.Id);
    }

    private sealed class BattleTargetChoice(Func<List<BattleTarget>> targets, Func<BattleTarget, ChangeEvent> outcome) : StepChoice
    {
        public override List<TargetRef> Options() => targets()
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

        protected override ChangeEvent Outcome(TargetRef target) => outcome(ToBattleTarget(target));

        private static TargetRef ToRef(BattleTarget t)
            => new(t.Type == TargetType.UNIT ? TargetKind.Unit : TargetKind.Country, t.Id);

        private static BattleTarget ToBattleTarget(TargetRef r)
            => new(r.Id, r.Kind == TargetKind.Unit ? TargetType.UNIT : TargetType.COUNTRY);
    }

    private sealed class FixedChoice(Func<ChangeEvent> outcome) : StepChoice
    {
        public override List<TargetRef> Options() => new();

        protected override Task<TargetRef> Prompt(Faction faction, List<TargetRef> options)
            => throw new InvalidOperationException("A fixed step has no prompt");

        protected override ChangeEvent Outcome(TargetRef target) => outcome();

        internal override async Task<CardStepResult> Run(Faction faction)
        {
            await WaitBeforePrompt();
            NotifyChosen(null);
            return outcome();
        }

        protected override void AddOutcomes(List<StepOption> into) => into.Add(new StepOption(null, outcome()));
    }
}
