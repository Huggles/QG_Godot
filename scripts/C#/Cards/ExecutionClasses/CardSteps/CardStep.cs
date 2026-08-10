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
    public int Id { get; set; } = 0;
    [JsonIgnore] public CardStep NextCardStep => CardLogic.CardSteps.ElementAtOrDefault(CardLogic.CardSteps.IndexOf(this) + 1);
    [JsonIgnore] public CardLogic CardLogic;    
    protected Func<Task<ChangeEvent>> StepLogic;
    protected Func<List<Condition>> GetConditionsMethod;
    protected Faction TriggeringFaction { get { return CardLogic.Faction; } }
    [JsonIgnore] protected List<Condition> Conditions => GetConditionsMethod != null ? GetConditionsMethod() : null;
    [JsonIgnore] public bool MeetAllConditions => Conditions != null ? Conditions.All(condition => condition.MeetCondition()) : true;

    public string ActionGuidance;

    public CardStep(CardLogic cardLogic, Func<Task<ChangeEvent>> stepLogic)
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

    public CardStep WithStepLogic(Func<Task<ChangeEvent>> stepLogic)
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

    public CardStep WithGuidance(string actionGuidance)
    {
        this.ActionGuidance = actionGuidance;
        return this;
    }

    public async Task<ChangeEvent> Execute()
    {
        StepFinished = true;
        bool CanExecuteStep = MeetAllConditions;
        ChangeEvent result = null;
        
        if (!CanExecuteStep)
        {
            //Should skip step
            DebugUtilities.PrintPeer("SKIPPING STEP");
            await new ShowActionLabelPresentationEvent(TriggeringFaction, "Unable to: " + ActionGuidance).Apply();
            await Task.Delay(GameSettings.DurationLong);
            if (NextCardStep != null)
            {
                result = await NextCardStep.Execute();
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
                result = await StepLogic.Invoke();
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
        
        return result;
    }

    public T BuildChangeEvent<T>(T changeEvent) where T : ChangeEvent
    {
        changeEvent.SourceCardId = CardLogic.CardState.Id;
        changeEvent.TriggeringFaction = CardLogic.CardState.Faction;
        return changeEvent;
    }

    public static List<CardStep> All => CardState.All.Values.SelectMany(cardState => cardState.CardLogic.CardSteps).ToList();
}
