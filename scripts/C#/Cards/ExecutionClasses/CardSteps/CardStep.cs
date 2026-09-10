using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public partial class CardStep : ITaggable
{
    [JsonIgnore] private readonly TagContainer _tags = new();
    [JsonIgnore] public TagContainer Tags => _tags;

    public bool StepFinished { get; set; } = false;

    /// <summary>
    /// True only once this step's logic has run to completion. Narrower than StepFinished, which is
    /// set before the logic runs and so means "done, no matter how": a step that was skipped —
    /// conditions not met, or the player cancelling its selection — is finished but not succeeded.
    /// Read by <see cref="RequiringPreviousStep"/> to keep the second half of a single action from
    /// happening on its own.
    /// </summary>
    public bool StepSucceeded { get; set; } = false;
    public int Id { get; set; } = 0;
    [JsonIgnore] public CardStep NextCardStep => CardLogic.CardSteps.ElementAtOrDefault(CardLogic.CardSteps.IndexOf(this) + 1);
    // ElementAtOrDefault returns null for a negative index, so the card's first step has none.
    [JsonIgnore] public CardStep PreviousCardStep => CardLogic.CardSteps.ElementAtOrDefault(CardLogic.CardSteps.IndexOf(this) - 1);
    [JsonIgnore] public CardLogic CardLogic;    
    protected Func<Task> StepLogic;
    protected Func<List<Condition>> GetConditionsMethod;
    protected Func<List<Condition>> GetAdvisoryConditionsMethod;
    private bool _requiresPreviousStep = false;
    protected Faction TriggeringFaction { get { return CardLogic.Faction; } }
    [JsonIgnore] protected List<Condition> Conditions => GetConditionsMethod != null ? GetConditionsMethod() : null;

    /// <summary>
    /// The prerequisite from <see cref="RequiringPreviousStep"/>. Folded into MeetAllConditions rather
    /// than into Conditions so the single gate that Execute and
    /// GameStateCalculator.CalculateExecutableStepsForFaction already consult stays the whole truth
    /// about whether this step can run.
    /// </summary>
    [JsonIgnore] private bool PreviousStepRequirementMet => !_requiresPreviousStep || (PreviousCardStep?.StepSucceeded ?? false);
    [JsonIgnore] public bool MeetAllConditions => (Conditions == null || Conditions.All(condition => condition.MeetCondition())) && PreviousStepRequirementMet;

    [JsonIgnore] protected List<Condition> AdvisoryConditions => GetAdvisoryConditionsMethod?.Invoke();

    /// <summary>
    /// Whether this step, if run right now, would do something worth doing — see
    /// <see cref="WithAdvisoryCondition"/>. Deliberately NOT folded into
    /// <see cref="MeetAllConditions"/>, unlike <see cref="PreviousStepRequirementMet"/> just above: an
    /// advisory condition must never stop the step or make it unexecutable, it only reports that the
    /// step's effect would be hollow. Read by
    /// GameStateCalculator.CalculateAttentionCardsForFaction to raise
    /// <see cref="Tag.NeedsAttention"/>, and by nothing in the execution path.
    /// </summary>
    [JsonIgnore] public bool MeetAllAdvisoryConditions =>
        AdvisoryConditions == null || AdvisoryConditions.All(condition => condition.MeetCondition());

    public string ActionGuidance;

    /// <summary>
    /// What this step's prompts are FOR, so a bot rule can tell a deploy-target country selection from
    /// the thirty-odd other reasons a card asks for a country. Declared with
    /// <see cref="WithPurpose"/>; <see cref="PromptPurpose.NONE"/> until it is.
    ///
    /// [JsonIgnore] is mandatory, not tidiness. Steps register themselves into
    /// GameSession.Current.GameState.CardSteps, and Id / StepFinished / StepSucceeded / ActionGuidance
    /// are all public and serialised — so a bare public field here would enter the saved game and the
    /// state hash. This is a property of the AUTHORED CARD, identical on every peer and across every
    /// save, and it must never be state.
    /// </summary>
    [JsonIgnore] public PromptPurpose Purpose = PromptPurpose.NONE;

    public CardStep(CardLogic cardLogic, Func<Task> stepLogic)
    {
        this.CardLogic = cardLogic;
        this.StepLogic = stepLogic;
        GameSession.Current.GameState.CardSteps.Add(this);
    }    

    public CardStep WithId(int id)
    {
        this.Id = id;
        return this;
    }

    public CardStep WithStepLogic(Func<Task> stepLogic)
    {
        this.StepLogic = stepLogic;
        return this;
    }

    public CardStep WithConditions(Func<List<Condition>> getConditionsMethod)
    {
        this.GetConditionsMethod = getConditionsMethod;
        return this;
    }
    public CardStep WithCondition(Func<Condition> getConditionMethod)
    {
        this.GetConditionsMethod = () => new List<Condition> { getConditionMethod() };
        return this;
    }

    /// <summary>
    /// An advisory condition: when it is NOT met the step still runs exactly as before, but the card is
    /// flagged <see cref="Tag.NeedsAttention"/> and drawn with a caution scrim so the player notices
    /// that the play, while legal, would achieve nothing on the current board.
    ///
    /// Use it for "the effect is hollow", never for "the effect is illegal" — that is
    /// <see cref="WithCondition"/>. A build card whose only targets are countries the faction already
    /// occupies is the motivating case: CountryState.CanBuild permits the deploy, GameAPI rebuilds in
    /// place, and the board is identical afterwards.
    /// </summary>
    public CardStep WithAdvisoryCondition(Func<Condition> getConditionMethod)
    {
        this.GetAdvisoryConditionsMethod = () => new List<Condition> { getConditionMethod() };
        return this;
    }

    /// <inheritdoc cref="WithAdvisoryCondition"/>
    public CardStep WithAdvisoryConditions(Func<List<Condition>> getConditionsMethod)
    {
        this.GetAdvisoryConditionsMethod = getConditionsMethod;
        return this;
    }

    /// <summary>
    /// Declare what this step's prompts are for. Sibling of <see cref="WithGuidance"/>, and the
    /// sturdy counterpart to it: guidance is prose for the player and changes with a copy edit, this
    /// is an enum the compiler checks.
    ///
    /// One purpose per step. Where a single step raises prompts of two different kinds — see
    /// EventGunsandButter — leave this unset and wrap each branch in
    /// <see cref="PromptOrigin.Narrow"/> instead, so the declaration sits where the truth is.
    /// </summary>
    public CardStep WithPurpose(PromptPurpose purpose)
    {
        this.Purpose = purpose;
        return this;
    }

    public CardStep WithGuidance(string actionGuidance)
    {
        this.ActionGuidance = actionGuidance;
        return this;
    }

    /// <summary>
    /// This step is one half of a single action and must not happen on its own: if the step before it
    /// in the card was skipped — its conditions not met, or the player cancelling its selection — this
    /// step is skipped too, quietly, which for a two-step card ends the card.
    ///
    /// Without it, a card that eliminates one of your units and then rebuilds it elsewhere hands out a
    /// free unit whenever the removal half is skipped, because the two steps are otherwise independent.
    /// </summary>
    public CardStep RequiringPreviousStep()
    {
        this._requiresPreviousStep = true;
        return this;
    }

    public async Task Execute()
    {
        // Publish which step is running so SendInputRequest can stamp it onto every prompt raised
        // below. A `using` DECLARATION, so the compiler wraps the whole rest of the method in the
        // try/finally this method otherwise lacks: the frame is restored on the normal path, on the
        // StepSkippedException path, and before the rethrow in the general catch. See PromptOrigin for
        // why restoring the displaced frame — rather than clearing — is the load-bearing part.
        using PromptOrigin.Scope origin = PromptOrigin.Enter(this);

        StepFinished = true;
        // A re-run — a Status card's steps are re-armed every turn — must not inherit the last run's result.
        StepSucceeded = false;
        bool CanExecuteStep = MeetAllConditions;

        if (!CanExecuteStep)
        {
            //Should skip step
            DebugUtilities.PrintPeer("SKIPPING STEP");
            // A step skipped only because its prerequisite step did not happen says nothing: the player
            // was already told about that step, and "Unable to: rebuild the unit" on top of it reads as
            // a second, separate failure.
            if (PreviousStepRequirementMet)
            {
                await new ShowActionLabelPresentationEvent(TriggeringFaction, "Unable to: " + ActionGuidance).Apply();
                await Task.Delay(GameSettings.DurationLong);
            }
            if (NextCardStep != null)
            {
                await NextCardStep.Execute();
            }
            else
            {
                DebugUtilities.PrintPeer("NO MORE STEPS LEFT");
            }
        }
        else
        {
            try
            {
                DebugUtilities.PrintPeer($"Invoking step: {CardLogic.CardState.CardName}");
                await new ShowActionLabelPresentationEvent(TriggeringFaction, ActionGuidance).Apply();
                ErrorInjection.MaybeThrow(ErrorInjection.Site.CardStep, CardLogic?.CardState?.CardName);
                await StepLogic();
                StepSucceeded = true;
            }
            catch (StepSkippedException)
            {
                // Player chose to skip this step's selection. Only the current step is
                // abandoned (result stays null so no change event fires); StepFinished is
                // already true, so DoCard advances to the card's next step (if any).
                DebugUtilities.PrintPeer("Player skipped step");
                await new ShowActionLabelPresentationEvent(TriggeringFaction, "Skipped: " + ActionGuidance).Apply();
            }
            catch (Exception e)
            {
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
        
        // Recalculate game state after step completes
        // Note: ChangeEvent.Apply() also recalculates, but this ensures tags are fresh
        // for any immediate condition checks or UI updates
        GameStateCalculator.CalculateAll();
    }

    public T BuildChangeEvent<T>(T changeEvent) where T : ChangeEvent
    {
        changeEvent.SourceCardId = CardLogic.CardState.Id;
        changeEvent.TriggeringFaction = CardLogic.CardState.Faction;
        return changeEvent;
    }

    public static List<CardStep> All => CardState.All.Values.SelectMany(cardState => cardState.CardLogic.CardSteps).ToList();
}
