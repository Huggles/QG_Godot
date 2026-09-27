using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// A step whose whole effect is ONE ChangeEvent. The card's body builds and returns it; provenance,
/// <c>IsTrigger</c> and the dispatch route are the step TYPE's business and are applied here, once,
/// for every card in the game.
///
/// The two concrete kinds differ in exactly two declarations — <see cref="EventIsTrigger"/> and
/// <see cref="DispatchEventAsync"/> — which is the whole of the distinction that used to be spelled
/// out by hand at every call site as an <c>IsTrigger =</c> line plus a choice between
/// <c>CardPlayPool.DoChangeEvent(e)</c> and <c>e.Apply()</c>.
/// </summary>
public abstract partial class EventCardStep : CardStep
{
    private readonly Func<Task<CardStepResult>> _produce;
    private readonly StepChoice _choice;

    protected EventCardStep(CardLogic cardLogic, Func<Task<CardStepResult>> produce) : base(cardLogic)
        => _produce = produce;

    /// <summary>A step declared as options plus an outcome factory, so its outcomes can be listed without running it.</summary>
    protected EventCardStep(CardLogic cardLogic, StepChoice choice) : base(cardLogic)
    {
        _choice = choice;
        _produce = async () =>
        {
            StepOption chosen = await choice.Run(TriggeringFaction, PreviousOutcome);
            LastOutcome = chosen;
            return chosen.Event;
        };
    }

    /// <summary>Stamped as dispatch would stamp them, so a projection sees the source card a discard modifier reads.</summary>
    public override IReadOnlyList<StepOption> PossibleOutcomes(StepOption? previous)
    {
        IReadOnlyList<StepOption> outcomes = _choice?.Outcomes(previous);
        if (outcomes != null)
            foreach (StepOption outcome in outcomes)
                if (outcome.Event != null) StampProvenance(outcome.Event);
        return outcomes;
    }

    /// <summary>Set on every event this step emits. The type decides; the card may not.</summary>
    protected abstract bool EventIsTrigger { get; }

    protected abstract Task DispatchEventAsync(ChangeEvent changeEvent);

    /// <summary>
    /// None: the event this step dispatched has already run CalculateAll at its own declared scope
    /// (see ChangeEvent.Apply), so the pass that closes Execute would be a second, FULL-scope
    /// recalculation on top of it -- after every event-producing step in the game.
    ///
    /// Earned, not argued. Narrowing this while the block and effect cards were still untyped
    /// changed the outcome of 14 games in 800: those cards mutate IsBlocked and ImmuneForTurn
    /// directly, outside any ChangeEvent, and were relying on the blanket recalculation. With them
    /// on BlockStep and EffectStep, which declare what they actually touch, the same 800-game batch
    /// is unchanged by this line.
    ///
    /// The case to keep an eye on is a ResultStep whose event is BLOCKED and so never reaches
    /// Apply(). Tags are still fresh there -- the blocking card ran its own DoCard, every step of
    /// which recalculates, and RequestBlockReactions calls CalculateAll(Flow) once per team turn of
    /// the window. If that ever stops holding, return All when the dispatched event came back
    /// IsBlocked rather than widening this for every step.
    /// </summary>
    public override RecalcScope RecalcScope => RecalcScope.None;

    protected sealed override Task<CardStepResult> RunCoreAsync() => _produce();

    protected sealed override async Task DispatchAsync(CardStepResult result)
    {
        // "The step ran and there was nothing to do" — see CardStepResult.Nothing. StepSucceeded
        // still becomes true, which is what a bare `return` out of the old Func<Task> lambda did and
        // what StatusBiasForAction and friends rely on.
        if (result.Outcome == StepOutcome.Nothing) return;

        if (result.Outcome != StepOutcome.Event) { ReportWrongArm(result); return; }

        StampProvenance(result.ChangeEvent);
        await DispatchEventAsync(result.ChangeEvent);
    }

    /// <summary>
    /// Where SourceCardId comes from, for every card, unconditionally.
    ///
    /// This is the one place it is set now, and that is the point rather than tidiness. It used to be
    /// stamped per call site by <c>CardLogic.BuildChangeEvent</c>, and eleven events across the EW
    /// submarine cards and both Plunders were built without it and shipped
    /// <c>SourceCardId = -1</c>. Three things broke quietly as a result:
    ///
    ///   - CardPlayRound.RegisterChangeEvent adds the source card to CardPool only when
    ///     sourceCardId > -1, so a card whose first event was one of those never joined the pool and
    ///     ContinueWithNextSteps could not resume it. That is a hard prerequisite for splitting those
    ///     very cards into two steps.
    ///   - Condition.IsBlockRequest reads CurrentBlockTrigger?.SourceCardState?.CardData?.CardType,
    ///     which null-chains to false, so ResponseASWTactics was never offered against any of them.
    ///   - CardPlayRound.GetTriggerCardId fell back to "last card in the pool", so a reaction prompt
    ///     showed the wrong card beside the event.
    ///
    /// With stamping central there is no per-card call site left to forget, so the bug class is gone
    /// structurally rather than fixed eleven times.
    /// </summary>
    private void StampProvenance(ChangeEvent changeEvent)
    {
        changeEvent.SourceCardId = CardLogic.CardState.Id;
        changeEvent.IsTrigger = EventIsTrigger;

        // Every ChangeEvent constructor takes its triggering faction (ChangeEvent(Faction) ->
        // GameMessage) and every card passes its own, so this is a backstop rather than the normal
        // path. Deliberately only FILLS an unset value and never overwrites a set one: a card acting
        // ON another faction says so through TargetFaction (ForceDiscardCardsChangeEvent(Faction,
        // Faction.UNITED_KINGDOM, n) is still triggered BY the card's owner), so a disagreement here
        // would mean something deliberate that this must not silently rewrite.
        if (changeEvent.TriggeringFaction == Faction.NONE)
            changeEvent.TriggeringFaction = CardLogic.Faction;
    }
}
