using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

/// <summary>
/// One step of a card's effect. Abstract: a step declares WHAT IT IS by its type — see
/// <see cref="StepKind"/> and the concrete subclasses — and the type, not a line inside the body,
/// decides <c>IsTrigger</c>, the dispatch route and the recalculation scope.
///
/// The body returns a <see cref="CardStepResult"/> and dispatches nothing itself. That is the whole
/// point of the split: a step used to be a <c>Func&lt;Task&gt;</c> that emitted its own events, so
/// nothing stopped one step doing two things, and 32 of them did. A cost and the effect it paid for
/// were one opaque body distinguished only by an <c>IsTrigger</c> flag and statement order, which
/// made both invisible to the three mechanisms that work at step granularity: reaction windows,
/// <c>CardPlayRound.ContinueWithNextSteps</c> re-entry, and
/// <see cref="CardStepBuilders.RequiringPreviousStep"/>.
/// </summary>
public abstract partial class CardStep : ITaggable
{
    [JsonIgnore] private readonly TagContainer _tags = new();
    [JsonIgnore] public TagContainer Tags => _tags;

    public bool StepFinished { get; set; } = false;

    /// <summary>
    /// True only once this step's logic has run to completion. Narrower than StepFinished, which is
    /// set before the logic runs and so means "done, no matter how": a step that was skipped —
    /// conditions not met, or the player cancelling its selection — is finished but not succeeded.
    /// Read by <see cref="CardStepBuilders.RequiringPreviousStep"/> to keep the second half of a
    /// single action from happening on its own.
    ///
    /// It means "the body ran and its result was dispatched", NOT "the effect landed": a blocked
    /// event still leaves this true, so the effect half of a cost+effect card still runs after a
    /// blocked cost — which is what the fused single step did before the two were split.
    ///
    /// **When it flips is load-bearing**, and not only for the prerequisite check. It is set only
    /// after dispatch and every window that dispatch opened have fully returned, so while step N's
    /// after-reaction window is open, a step N+1 gated on it is not executable. That is what stops
    /// CardPlayRound.ContinueWithNextSteps hoisting the second half of a split card into the first
    /// half's reaction window and emitting an extra ActivateReactionChangeEvent into the replicated
    /// stream, where today's fused step emits its second event strictly after the window closes.
    /// </summary>
    public bool StepSucceeded { get; set; } = false;

    /// <summary>
    /// Position in <c>GameState.CardSteps</c>, assigned by the constructor.
    ///
    /// This used to be 0 for every step in the game: a <c>WithId()</c> builder existed and no card
    /// ever called it. So <see cref="PromptOrigin"/>'s Frame.StepId and InputRequest.OriginStepId
    /// carried 0 game-wide — a prompt could not say WHICH step of a card raised it — ErrorReporter's
    /// <c>CardStep "name" #0</c> named no step, and MultiplayerGameState.CardStepsById would have
    /// thrown on a duplicate key had anything ever read it.
    ///
    /// Deterministic: steps are constructed in a fixed order during setup, so the same step has the
    /// same id on every peer and across a replayed save.
    /// </summary>
    public int Id { get; set; } = 0;

    public string ActionGuidance;

    // ElementAtOrDefault returns null for a negative index, so the card's first step has none.
    [JsonIgnore] public CardStep NextCardStep => CardLogic.CardSteps.ElementAtOrDefault(CardLogic.CardSteps.IndexOf(this) + 1);
    [JsonIgnore] public CardStep PreviousCardStep => CardLogic.CardSteps.ElementAtOrDefault(CardLogic.CardSteps.IndexOf(this) - 1);
    [JsonIgnore] public CardLogic CardLogic;

    // internal, not private: the fluent builders are generic extension methods — see
    // CardStepBuilders for why they cannot be instance methods. One assembly, so internal costs
    // nothing, and System.Text.Json ignores non-public members, which strengthens the rule on
    // Purpose below that authored card data must never enter the saved game.
    [JsonIgnore] internal Func<List<Condition>> GetConditionsMethod;
    [JsonIgnore] internal Func<List<Condition>> GetAdvisoryConditionsMethod;
    [JsonIgnore] internal bool RequiresPreviousStep;

    [JsonIgnore] protected Faction TriggeringFaction => CardLogic.Faction;
    [JsonIgnore] protected List<Condition> Conditions => GetConditionsMethod?.Invoke();

    /// <summary>
    /// The prerequisite from <see cref="CardStepBuilders.RequiringPreviousStep"/>. Folded into
    /// MeetAllConditions rather than into Conditions so the single gate that Execute and
    /// GameStateCalculator.EvaluateExecutableSteps already consult stays the whole truth about
    /// whether this step can run.
    /// </summary>
    [JsonIgnore] private bool PreviousStepRequirementMet => !RequiresPreviousStep || (PreviousCardStep?.StepSucceeded ?? false);

    [JsonIgnore] public bool MeetAllConditions
    {
        get
        {
            // Resolved ONCE. GetConditionsMethod is a card author's lambda that allocates a fresh
            // List<Condition> of fresh Condition objects on every call, and the expression this
            // replaces read the property twice — once for the null test, once for the All() — so
            // every evaluation built the list twice and threw one copy away.
            List<Condition> conditions = Conditions;
            return (conditions == null || conditions.All(condition => condition.MeetCondition()))
                   && PreviousStepRequirementMet;
        }
    }

    [JsonIgnore] protected List<Condition> AdvisoryConditions => GetAdvisoryConditionsMethod?.Invoke();

    /// <summary>
    /// Whether this step, if run right now, would do something worth doing — see
    /// <see cref="CardStepBuilders.WithAdvisoryCondition"/>. Deliberately NOT folded into
    /// <see cref="MeetAllConditions"/>, unlike <see cref="PreviousStepRequirementMet"/> just above: an
    /// advisory condition must never stop the step or make it unexecutable, it only reports that the
    /// step's effect would be hollow. Read by
    /// GameStateCalculator.CalculateAttentionCardsForFaction to raise
    /// <see cref="Tag.NeedsAttention"/>, and by nothing in the execution path.
    /// </summary>
    [JsonIgnore] public bool MeetAllAdvisoryConditions
    {
        get
        {
            List<Condition> advisory = AdvisoryConditions;   // once — see MeetAllConditions
            return advisory == null || advisory.All(condition => condition.MeetCondition());
        }
    }

    /// <summary>
    /// What this step's prompts are FOR, so a bot rule can tell a deploy-target country selection from
    /// the thirty-odd other reasons a card asks for a country. Declared with
    /// <see cref="CardStepBuilders.WithPurpose"/>; <see cref="PromptPurpose.NONE"/> until it is.
    ///
    /// [JsonIgnore] is mandatory, not tidiness. Steps register themselves into
    /// GameSession.Current.GameState.CardSteps, and Id / StepFinished / StepSucceeded / ActionGuidance
    /// are all public and serialised — so a bare public field here would enter the saved game and the
    /// state hash. This is a property of the AUTHORED CARD, identical on every peer and across every
    /// save, and it must never be state. The same rule governs <see cref="Kind"/> and
    /// <see cref="RecalcScope"/> below.
    /// </summary>
    [JsonIgnore] public PromptPurpose Purpose = PromptPurpose.NONE;

    /// <summary>What kind of step this is, structurally. See <see cref="StepKind"/>.</summary>
    [JsonIgnore] public abstract StepKind Kind { get; }

    /// <summary>
    /// What this step disturbs, for the <c>CalculateAll()</c> that closes <see cref="Execute"/>.
    ///
    /// Declared per TYPE and pessimistic by default, exactly as <see cref="ChangeEvent.RecalcScope"/>
    /// is and for the same reason: getting one wrong leaves a stale tag rather than throwing, so
    /// narrowing has to be opt-in and one small reviewable claim at a time.
    ///
    /// The two event-producing kinds narrow to <see cref="RecalcScope.None"/> — their event's own
    /// <c>Apply()</c> already ran CalculateAll at the event's own declared scope, and this call used
    /// to run a SECOND, full-scope pass on top of it after every step in the game.
    /// </summary>
    [JsonIgnore] public virtual RecalcScope RecalcScope => RecalcScope.All;

    protected CardStep(CardLogic cardLogic)
    {
        this.CardLogic = cardLogic;
        Id = GameSession.Current.GameState.CardSteps.Count;
        GameSession.Current.GameState.CardSteps.Add(this);
    }

    /// <summary>
    /// Run the card author's body and hand back what it produced. The subclass owns this because the
    /// delegate's shape is the subclass's business — <see cref="BlockStep{TTrigger}"/> passes the
    /// typed trigger in, every other kind takes no argument.
    /// </summary>
    protected abstract Task<CardStepResult> RunCoreAsync();

    /// <summary>
    /// What this step actually did the last time it ran as a choice step: the answer and its event.
    /// Overwritten only by a completed choice, so it keeps the same lifetime as the card fields it
    /// replaces. Not state — a save is only taken between cards, where no step is waiting on it.
    /// </summary>
    [JsonIgnore] public StepOption? LastOutcome { get; internal set; }

    /// <summary>The previous step's real outcome in this activation, or null when it has not run.</summary>
    [JsonIgnore] public StepOption? PreviousOutcome
        => PreviousCardStep is { StepSucceeded: true } previous ? previous.LastOutcome : null;

    /// <summary>
    /// Every event this step could produce right now, one per legal answer, without prompting or
    /// applying anything — or null when the step is free-form or cannot say. Does not check the
    /// step's conditions. The caller owns the events: <see cref="StepChoice.Release"/> them.
    /// </summary>
    public IReadOnlyList<StepOption> PossibleOutcomes() => PossibleOutcomes(PreviousOutcome);

    /// <summary>As above, after a hypothetical <paramref name="previous"/> — how a projection chains steps.</summary>
    public virtual IReadOnlyList<StepOption> PossibleOutcomes(StepOption? previous) => null;

    /// <summary>The cards a play step could play after <paramref name="previous"/>, or null when it cannot say. See <see cref="PlayChoice"/>.</summary>
    public virtual IReadOnlyList<int> PossiblePlays(StepOption? previous) => null;

    /// <summary>
    /// Apply the result. One seam per step kind, and the only place a step's effect reaches the game.
    /// </summary>
    protected abstract Task DispatchAsync(CardStepResult result);

    /// <summary>
    /// A result arm this step kind cannot dispatch. Reported and ignored rather than thrown: the
    /// price of the uniform <see cref="CardStepResult"/> is that this is a runtime question, and
    /// turning a card-authoring slip into a halted turn loop would be a worse answer than a loud log
    /// line naming the card and the step.
    /// </summary>
    protected void ReportWrongArm(CardStepResult result)
        => DebugUtilities.PrintPeerError(
            $"{CardLogic?.CardState?.CardName} #{Id}: a {Kind} step returned {result.Outcome}, which it cannot dispatch");

    public async Task Execute()
    {
        // Publish which step is running so SendInputRequest can stamp it onto every prompt raised
        // below. A `using` DECLARATION, so the compiler wraps the whole rest of the method in the
        // try/finally this method otherwise lacks: the frame is restored on the normal path, on the
        // StepSkippedException path, and before the rethrow in the general catch. See PromptOrigin for
        // why restoring the displaced frame — rather than clearing — is the load-bearing part.
        //
        // Dispatch happens INSIDE this scope, deliberately: a cost event raises its own prompt from
        // inside ExecuteAsync (ForceDiscardHandCardsChangeEvent does), and that prompt has to carry
        // this step's card id, step id and purpose.
        using PromptOrigin.Scope origin = PromptOrigin.Enter(this);

        StepFinished = true;
        // A re-run — a Status card's steps are re-armed every turn — must not inherit the last run's result.
        StepSucceeded = false;

        if (!MeetAllConditions)
        {
            //Should skip step
            DebugUtilities.PrintPeer("SKIPPING STEP");
            // A step skipped only because its prerequisite step did not happen says nothing: the player
            // was already told about that step, and "Unable to: rebuild the unit" on top of it reads as
            // a second, separate failure.
            if (PreviousStepRequirementMet && !string.IsNullOrEmpty(ActionGuidance))
            {
                await new ShowActionLabelPresentationEvent(TriggeringFaction, "Unable to: " + ActionGuidance).Apply();
                await Task.Delay(GameSettings.DurationLong);
            }
            // No cascade to NextCardStep.Execute(). CardPlayRound.DoCard's while loop owns
            // advancement, and the cascade this replaces was a second, weaker copy of it: it did not
            // re-check cardLogic.IsBlocked, so a card blocked mid-resolution kept running its
            // remaining steps; it made stack depth grow with the step count; and it was one of the
            // two reentrancy cases PromptOrigin has to defend against by name. DoCard captures the
            // same List reference and re-queries !StepFinished every iteration, so removing it
            // changes neither which steps run nor their order.
        }
        else
        {
            try
            {
                DebugUtilities.PrintPeer($"Invoking step: {CardLogic.CardState.CardName} #{Id} ({Kind})");
                // Guarded on guidance being set, which is what makes splitting a fused step free:
                // the cost half keeps the card's single label and the effect half declares none, so
                // the player still sees exactly one line per action rather than two. It also stops
                // the handful of steps that never had a .WithGuidance at all from announcing
                // themselves with an empty label.
                if (!string.IsNullOrEmpty(ActionGuidance))
                    await new ShowActionLabelPresentationEvent(TriggeringFaction, ActionGuidance).Apply();
                ErrorInjection.MaybeThrow(ErrorInjection.Site.CardStep, CardLogic?.CardState?.CardName);
                await DispatchAsync(await RunCoreAsync());
                StepSucceeded = true;
            }
            catch (StepSkippedException)
            {
                // Player chose to skip this step's selection. Only the current step is
                // abandoned (nothing is dispatched, so no change event fires); StepFinished is
                // already true, so DoCard advances to the card's next step (if any).
                DebugUtilities.PrintPeer("Player skipped step");
                await new ShowActionLabelPresentationEvent(TriggeringFaction, "Skipped: " + ActionGuidance).Apply();
            }
            catch (Exception e) when (!ErrorReporter.IsBenign(e))
            {
                // An exception filter, so a benign unwind — a stale epoch, an abandoned session — never
                // runs this body at all and the stack is left undisturbed for whoever does handle it.
                //
                // Report here, where the card and step are known, then RETHROW.
                //
                // This used to swallow the exception and let DoCard move to the card's next step. That
                // looked like a recovery but is not one: the player's action silently does not happen
                // and the board no longer matches what the card said it would do. Letting it escape to
                // the Guard on the turn step halts the loop and puts the decision in the player's
                // hands via the popup's Continue button, which is the honest outcome.
                //
                // Marked as reported so the Guard above does not report the same failure twice — the
                // context here (card name + step id) is richer than anything it could reconstruct.
                ErrorReporter.Report(e, $"CardStep \"{CardLogic?.CardState?.CardName}\" #{Id}", TriggeringFaction);
                ErrorReporter.MarkReported(e);
                throw;
            }
        }

        // After the if/else and deliberately NOT in a finally: the rethrow path above must escape
        // before this runs, as it always has.
        //
        // Scoped by the step's kind rather than the blanket CalculateAll() this replaces. For a step
        // whose whole effect is one ChangeEvent that is RecalcScope.None, because the event's own
        // Apply() already recalculated at its own declared scope — this was a second, full-scope
        // pass on top of it, after every step in the game.
        GameStateCalculator.CalculateAll(RecalcScope);
    }

    public static List<CardStep> All => CardState.All.Values.SelectMany(cardState => cardState.CardLogic.CardSteps).ToList();
}
