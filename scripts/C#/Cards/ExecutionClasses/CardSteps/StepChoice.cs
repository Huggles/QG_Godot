using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// One legal answer to a step's prompt and the ChangeEvent it would produce. <see cref="Target"/> is
/// null for a step that asks nothing (<see cref="Choose.Fixed(Func{StepContext, ChangeEvent})"/>).
/// </summary>
public readonly record struct StepOption(TargetRef? Target, ChangeEvent Event);

/// <summary>
/// What a step's options and outcome may read: the situation it runs in, and the previous step's
/// outcome. On the real path that is the live game and what the previous step actually did; in a
/// projection, a forked board and the hypothetical previous outcome. Null <see cref="Previous"/> means
/// "not known" — dereference it with <c>.Value</c> and <see cref="StepChoice.Outcomes"/> reports
/// unknown rather than a guess.
/// </summary>
public readonly record struct StepContext(GameSituation Situation, StepOption? Previous)
{
    public BoardState Board => Situation.Board;

    /// <summary>The live game, after <paramref name="previous"/>.</summary>
    public static StepContext Live(StepOption? previous) => new(GameSituation.Live, previous);
}

/// <summary>
/// A step's body split into its two halves: what the player may choose, and a PURE factory from one
/// choice to the ChangeEvent it causes. The step prompts in between. <see cref="Outcomes"/> skips the
/// prompt and runs the factory on every option — that is how a step's effect is seen before it runs.
///
/// Both halves read a <see cref="StepContext"/>, never the live game directly, so the same factory
/// answers for the real run and for a forked board. Card state that follows a choice goes in
/// <see cref="OnChosen"/>.
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
    public abstract List<TargetRef> Options(StepContext context);

    protected abstract Task<TargetRef> Prompt(Faction faction, List<TargetRef> options);

    protected abstract ChangeEvent Outcome(TargetRef target, StepContext context);

    /// <summary>The real path: prompt, record the choice, build its event. A skip throws StepSkippedException, as before.</summary>
    internal virtual async Task<StepOption> Run(Faction faction, StepContext context)
    {
        if (_beforePrompt != null) await _beforePrompt();
        TargetRef chosen = await Prompt(faction, Options(context));
        _onChosen?.Invoke(chosen);
        // Built against the situation AFTER the prompt: answering may have moved the round on.
        return new StepOption(chosen, Outcome(chosen, context with { Situation = context.Situation.Board.IsLive ? GameSituation.Live : context.Situation }));
    }

    /// <summary>
    /// Every option with the event it would produce. Nothing is prompted, applied or numbered. The
    /// events are native Godot objects: free the ones you do not apply with <see cref="Release"/>.
    /// Null when a factory throws — it may need context only the real run has, or a previous outcome
    /// that is not known.
    /// </summary>
    public IReadOnlyList<StepOption> Outcomes(StepContext context)
    {
        List<StepOption> outcomes = new();
        try
        {
            AddOutcomes(outcomes, context);
            return outcomes;
        }
        catch (Exception)
        {
            Release(outcomes);
            return null;
        }
    }

    protected virtual void AddOutcomes(List<StepOption> into, StepContext context)
    {
        foreach (TargetRef target in Options(context)) into.Add(new StepOption(target, Outcome(target, context)));
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
///
/// The lambdas take a <see cref="StepContext"/> and read the board and trigger through it, never the
/// live game, so the same step answers for a forked board.
/// </summary>
public static class Choose
{
    public static StepChoice CountryFrom(Func<StepContext, List<int>> countryIds, Func<int, StepContext, ChangeEvent> outcome)
        => new CountryChoice(countryIds, outcome);

    public static StepChoice UnitFrom(Func<StepContext, List<int>> unitIds, Func<int, StepContext, ChangeEvent> outcome, bool allowSkip = true)
        => new UnitChoice(unitIds, outcome, allowSkip);

    public static StepChoice FactionFrom(Func<StepContext, List<Faction>> factions, Func<Faction, StepContext, ChangeEvent> outcome)
        => new FactionChoice(factions, outcome);

    public static StepChoice BattleTargetFrom(Func<StepContext, List<BattleTarget>> targets, Func<BattleTarget, StepContext, ChangeEvent> outcome)
        => new BattleTargetChoice(targets, outcome);

    /// <summary>No prompt: the step has exactly one outcome.</summary>
    public static StepChoice Fixed(Func<StepContext, ChangeEvent> outcome) => new FixedChoice(outcome);

    private sealed class CountryChoice(Func<StepContext, List<int>> ids, Func<int, StepContext, ChangeEvent> outcome) : StepChoice
    {
        public override List<TargetRef> Options(StepContext context)
            => ids(context).Select(id => new TargetRef(TargetKind.Country, id)).ToList();

        protected override async Task<TargetRef> Prompt(Faction faction, List<TargetRef> options)
        {
            InputRequest response = await new InputRequest.SelectCountryRequestHandler(faction, options.Select(o => o.Id).ToList()).BroadCast();
            return new TargetRef(TargetKind.Country, response.ResponseCountryIds[0]);
        }

        protected override ChangeEvent Outcome(TargetRef target, StepContext context) => outcome(target.Id, context);
    }

    private sealed class UnitChoice(Func<StepContext, List<int>> ids, Func<int, StepContext, ChangeEvent> outcome, bool allowSkip) : StepChoice
    {
        public override List<TargetRef> Options(StepContext context)
            => ids(context).Select(id => new TargetRef(TargetKind.Unit, id)).ToList();

        protected override async Task<TargetRef> Prompt(Faction faction, List<TargetRef> options)
        {
            InputRequest response = await new InputRequest.SelectUnitRequestHandler(faction, options.Select(o => o.Id).ToList(), allowSkip).BroadCast();
            return new TargetRef(TargetKind.Unit, response.ResponseUnitIds[0]);
        }

        protected override ChangeEvent Outcome(TargetRef target, StepContext context) => outcome(target.Id, context);
    }

    private sealed class FactionChoice(Func<StepContext, List<Faction>> factions, Func<Faction, StepContext, ChangeEvent> outcome) : StepChoice
    {
        public override List<TargetRef> Options(StepContext context)
            => factions(context).Select(f => new TargetRef(TargetKind.Faction, (int)f)).ToList();

        // SelectFactionRequestHandler answers in ResponseCardIds — see its Handle().
        protected override async Task<TargetRef> Prompt(Faction faction, List<TargetRef> options)
        {
            InputRequest response = await new InputRequest.SelectFactionRequestHandler(faction, options.Select(o => (Faction)o.Id).ToList()).BroadCast();
            return new TargetRef(TargetKind.Faction, response.ResponseCardIds[0]);
        }

        protected override ChangeEvent Outcome(TargetRef target, StepContext context) => outcome((Faction)target.Id, context);
    }

    private sealed class BattleTargetChoice(Func<StepContext, List<BattleTarget>> targets, Func<BattleTarget, StepContext, ChangeEvent> outcome) : StepChoice
    {
        public override List<TargetRef> Options(StepContext context) => targets(context)
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

        protected override ChangeEvent Outcome(TargetRef target, StepContext context) => outcome(ToBattleTarget(target), context);

        private static TargetRef ToRef(BattleTarget t)
            => new(t.Type == TargetType.UNIT ? TargetKind.Unit : TargetKind.Country, t.Id);

        private static BattleTarget ToBattleTarget(TargetRef r)
            => new(r.Id, r.Kind == TargetKind.Unit ? TargetType.UNIT : TargetType.COUNTRY);
    }

    private sealed class FixedChoice(Func<StepContext, ChangeEvent> outcome) : StepChoice
    {
        public override List<TargetRef> Options(StepContext context) => new();

        protected override Task<TargetRef> Prompt(Faction faction, List<TargetRef> options)
            => throw new InvalidOperationException("A fixed step has no prompt");

        protected override ChangeEvent Outcome(TargetRef target, StepContext context) => outcome(context);

        internal override async Task<StepOption> Run(Faction faction, StepContext context)
        {
            await WaitBeforePrompt();
            NotifyChosen(null);
            return new StepOption(null, outcome(context.Situation.Board.IsLive ? context with { Situation = GameSituation.Live } : context));
        }

        protected override void AddOutcomes(List<StepOption> into, StepContext context)
            => into.Add(new StepOption(null, outcome(context)));
    }
}
