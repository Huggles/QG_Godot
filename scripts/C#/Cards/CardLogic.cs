using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;

public abstract partial class CardLogic : GodotObject
{
    public CardState CardState;
    public CardData CardData => CardState.CardData;
    public Faction Faction => CardState.Faction;
    public FactionData FactionData => StaticGameData.FactionDataMap.ContainsKey(Faction) ? StaticGameData.FactionDataMap[Faction] : null;

    public List<int> ActivatedInTurns = new();

    public bool IsPlayed;
    public bool IsActivatedOnce => ActivatedInTurns.Count > 0;
    public bool IsActivatedThisTurn => ActivatedInTurns.Contains(GameSession.Instance.GameFlow.GameTurn);
    public bool IsReaction => CardData.Type == "RESPONSE" || CardData.Type == "STATUS";
    public bool IsPubliclyVisible => IsPlayed || (CardData.Type == "RESPONSE" && IsActivatedOnce);
    public bool IsPlayFinished = false;
    public bool IsActivationFinished = false;

    [Signal] public delegate void CardFinishedEventHandler();
    [Signal] public delegate void CardStepFinishedEventHandler();
    
    public List<CardStep> PlayCardSteps;
    public List<CardStep> ReactCardSteps;
    public abstract List<CardStep> InitializePlayCardSteps();
    public virtual List<CardStep> InitializeReactCardSteps() { return new(); }    

    public bool CanPlayCard()
    {
        return !IsPlayed && !IsPlayFinished && ExecutablePlaySteps.Count > 0;
    }
    
    public bool CanBeActivated()
    {
        return !IsActivatedThisTurn && !IsActivationFinished && TriggerConditionsMet;
    }
    private bool TriggerConditionsMet
    {
        get { return CardTriggers().Count > 0 && CardTriggers().All(condition=>condition.MeetCondition()); }
    }

    protected virtual List<Condition> CardTriggers()
    {
        return new();
    }
    public List<CardStep> ExecutablePlaySteps
    {
        get {
            return PlayCardSteps.Where(playCardStep => !playCardStep.StepFinished && playCardStep.PrerequisiteStepFinished && playCardStep.MeetAllConditions).ToList();
        }        
    }
    public List<CardStep> ExecutableReactSteps
    {
        get {
            return ReactCardSteps.Where(reactCardStep => !reactCardStep.StepFinished && reactCardStep.PrerequisiteStepFinished && reactCardStep.MeetAllConditions).ToList();
        }        
    }

    public CardStep CardStepForId(int id)
    {
        CardStep cardStep = PlayCardSteps.Find(step => step.Id == id);
        if (cardStep == null)
        {
            cardStep = ReactCardSteps.Find(step => step.Id == id);
        }
        return cardStep;
    }

    public CardLogic()
    {
        PlayCardSteps = InitializePlayCardSteps();
        ReactCardSteps = InitializeReactCardSteps();
        for (int i = PlayCardSteps.Count - 1; i > 0; i--)
        {
            PlayCardSteps[i].IsPlayStep = true;
            if (PlayCardSteps[i].PrerequisiteCardStep == null)
            {
                PlayCardSteps[i].PrerequisiteCardStep = PlayCardSteps[i - 1];

            }
        }
        for (int i = ReactCardSteps.Count - 1; i > 0; i--)
        {
            ReactCardSteps[i].IsReactStep = true;
            if (ReactCardSteps[i].PrerequisiteCardStep == null)
            {
                ReactCardSteps[i].PrerequisiteCardStep = ReactCardSteps[i - 1];
            }
        }
        EventBus.Instance.NewTurnStarted += (turnNumber) => 
        {
            ReactCardSteps.ForEach(reactCardStep => reactCardStep.StepFinished = false);
        };
    }
    

    public void PlayCard(int stepId)
    {
        DebugUtilities.PrintPeer("PlayCard");
        string message = PlayActionGuidance();
        PlayerActionLabel.ShowText(message, -1, Faction);
        CardStepForId(stepId).Execute();
    }

    public async Task React(int stepId)
    {
        string message = ActivateActionGuidance();
        PlayerActionLabel.ShowText(message, -1, Faction);
        CardStepForId(stepId).Execute();        
    }

    public virtual string PlayActionGuidance() =>
        $"Play {GetType().Name}";

    public virtual string ActivateActionGuidance() =>
        $"Activate {GetType().Name}";

    public T BuildChangeEvent<T>(T changeEvent) where T : ChangeEvent
    {
        changeEvent.SourceCardId = this.CardState.Id;
        return changeEvent;
    }
}
