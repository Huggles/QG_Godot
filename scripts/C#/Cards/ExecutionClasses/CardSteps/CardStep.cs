using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public partial class CardStep
{
    public bool StepFinished { get; set; } = false;
    public bool IsPlayStep { get; set; } = false;
    public bool IsReactStep { get; set; } = false;
    public int Id { get; set; } = 0;
    [JsonIgnore] public CardStep NextCardStep
    {
        get
        {
            return IsPlayStep
            ? CardLogic.PlayCardSteps.ElementAtOrDefault(CardLogic.PlayCardSteps.IndexOf(this) + 1)
            : CardLogic.ReactCardSteps.ElementAtOrDefault(CardLogic.ReactCardSteps.IndexOf(this) + 1);
        }
    }

    [JsonIgnore] public CardLogic CardLogic;    
    protected Func<Task<ChangeEvent>> StepLogic;
    protected Func<List<Condition>> GetConditionsMethod;
    protected Func<Condition> GetConditionMethod;
    protected Faction TriggeringFaction { get { return CardLogic.Faction; } }
    [JsonIgnore] protected List<Condition> Conditions
    {
        get
        {
            if (GetConditionsMethod != null)
            {
                return GetConditionsMethod();
            }
            else if (GetConditionMethod != null)
            {
                return new List<Condition> { GetConditionMethod() };
            }
            return null;
        }
    }
    [JsonIgnore] public bool MeetAllConditions
    {
        get
        {
            return Conditions != null ? Conditions.All(condition => condition.MeetCondition()) : true;
        }
    }


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
        this.GetConditionMethod = getConditionMethod;
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
            PlayerActionLabel.ShowText("Unable to: " + ActionGuidance, -1, TriggeringFaction);
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
                PlayerActionLabel.ShowText(ActionGuidance, -1, TriggeringFaction);
                result = await StepLogic.Invoke();
            }
            catch (Exception e)
            {
                DebugUtilities.PrintPeer(e.Message);
            }
        }
        
        // Recalculate game state after step completes
        // Note: ChangeEvent.ApplyChange() also recalculates, but this ensures tags are fresh
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
}
