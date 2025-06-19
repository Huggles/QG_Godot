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
    protected Action StepLogic;
    protected Func<List<Condition>> GetConditionsMethod;
    protected Faction TriggeringFaction { get { return CardLogic.Faction; } }

    public CardStep(CardLogic cardLogic, Action stepLogic) : this(cardLogic, null, stepLogic) { }
    public CardStep(CardLogic cardLogic, CardStep prerequisite, Action stepLogic) : this(stepIdCounter, cardLogic, null, stepLogic)
    {
        stepIdCounter += 1;
    }
    public CardStep(int stepId, CardLogic cardLogic, CardStep prerequisite, Action stepLogic)
    {
        this.CardLogic = cardLogic;
        this.StepLogic = stepLogic;        
        this.PrerequisiteCardStep = prerequisite;
        this.Id = stepId;
        GameSession.Instance.GameState.CardStepsById[this.Id] = this;
    }

    public CardStep WithConditions(Func<List<Condition>> getConditionsMethod)
    {
        this.GetConditionsMethod = getConditionsMethod;
        return this;
    }

    public bool PrerequisiteStepFinished
    {
        get { return PrerequisiteCardStep != null ? PrerequisiteCardStep.StepFinished : true; }
    }

    public void Execute()
    {
        StepFinished = true;
        bool CanExecuteStep = true;
        if (GetConditionsMethod != null)
        {
            List<Condition> conditions = GetConditionsMethod();
            if (conditions.Count > 0) {
                CanExecuteStep = conditions.All((condition) => { return condition.MeetCondition(); });
            }
        }
        
        if (!CanExecuteStep)
        {
            //Should skip step
            DebugUtilities.PrintPeer("SKIPPING STEP");
            if (NextCardStep != null)
            {
                NextCardStep.Execute();
            }
            else
            {
                DebugUtilities.PrintPeer("NO MORE STEPS LEFT");
                CardPlayPool.DoNextActions();
            }

            return;
        }
        else
        {
            try
            {
                StepLogic.Invoke();
            }
            catch (Exception e)
            {
                DebugUtilities.PrintPeer(e.Message);
            }
        }
        
        
    }

    public T BuildChangeEvent<T>(T changeEvent) where T : ChangeEvent
    {
        changeEvent.SourceCardId = CardLogic.CardState.Id;
        changeEvent.TriggeringFaction = CardLogic.CardState.Faction;
        return changeEvent;
    }
}
