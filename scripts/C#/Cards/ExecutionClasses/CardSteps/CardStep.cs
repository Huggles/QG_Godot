using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class CardStep : GodotObject
{
    public static int stepIdCounter = 100;
    public static CardStep ForId(int id)
    {
        return GameSession.Instance.GameState.CardStepsById[id];
    }

    public bool StepFinished = false;
    public bool IsPlayStep = false;
    public bool IsReactStep = false;
    public int Id = 0;
    public CardStep PrerequisiteCardStep;
    public CardStep NextCardStep
    {
        get
        {
            return IsPlayStep
            ? CardLogic.PlayCardSteps.ElementAtOrDefault(CardLogic.PlayCardSteps.IndexOf(this) + 1)
            : CardLogic.ReactCardSteps.ElementAtOrDefault(CardLogic.ReactCardSteps.IndexOf(this) + 1);
        }
    }

    public CardLogic CardLogic;    
    protected Func<Task<ChangeEvent>> StepLogic;
    protected Func<List<Condition>> GetConditionsMethod;
    protected Func<Condition> GetConditionMethod;
    protected Faction TriggeringFaction { get { return CardLogic.Faction; } }
    protected List<Condition> Conditions
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
    public bool MeetAllConditions
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
        this.Id = stepIdCounter;
        stepIdCounter += 1;
        GameSession.Instance.GameState.CardStepsById[this.Id] = this;        
    }    

    public CardStep WithId(int id)
    {
        this.Id = id;
        return this;
    }

    public CardStep WithPrerequisiteStep(int stepId)
    {
        this.PrerequisiteCardStep = CardStep.ForId(stepId);
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


    public bool PrerequisiteStepFinished
    {
        get { return PrerequisiteCardStep != null ? PrerequisiteCardStep.StepFinished : true; }
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
            await Task.Delay(2000);
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
