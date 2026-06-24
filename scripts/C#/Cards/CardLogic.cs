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

    // IsPlayed is computed based on the card's tag
    
    
    public bool IsActivatedOnce => ActivatedInTurns.Count > 0;
    public bool IsActivatedThisTurn => ActivatedInTurns.Contains(GameFlow.Instance.GameTurn);
    public bool IsResponse => CardData.Type == "RESPONSE";
    public bool IsStatus => CardData.Type == "STATUS";
    public bool IsPubliclyVisible => CardState.IsPlayed || (IsResponse && IsActivatedOnce);
    public bool IsPlayFinished = false;
    public bool IsActivationFinished = false;
    public bool IsBlockReaction => CardTriggers().Any(triggerCondition => triggerCondition is Condition.IsBlockRequest);    
    public bool HasExecutableCardSteps => CardSteps.Count == 0 || ExecutableCardSteps.Count > 0;
    public int NextStepId => HasExecutableCardSteps ? ExecutableCardSteps[0].Id : -1;
    public virtual int MaxActivationsPerRound => 1;

    [Signal] public delegate void CardFinishedEventHandler();
    
    public List<CardStep> CardSteps = new();
    public abstract List<CardStep> OnActivate();
    
    public bool CanBeActivated() => !IsActivatedThisTurn && !IsActivationFinished && TriggerConditionsMet && HasExecutableCardSteps;
    
    public bool TriggerConditionsMet => _conditions.All(condition=>condition.MeetCondition());


    private List<Condition> _defaultPlayConditions => new List<Condition> 
    {
        new Condition.IsGameFlowStep(TurnStep.PLAY_CARD),
        new Condition.IsFactionTurn(Faction),
        new Condition.Not(new Condition.HasPlayedCardThisTurnStep(Faction))
    };
    

    private List<Condition> _conditions
    {
        get {  
            if(GameFlow.Instance.TurnStep == TurnStep.PLAY_CARD)
            {
                DebugUtilities.PrintPeer($"Checking play conditions for {CardData.Label} - IsPlayed: {CardState.IsPlayed}, IsFactionTurn: {GameFlow.Instance.CurrentFaction == Faction}, HasPlayedCardThisTurnStep: {new Condition.HasPlayedCardThisTurnStep(Faction).MeetCondition()}");
                DebugUtilities.PrintPeer($"Default play conditions met: {_defaultPlayConditions.All(condition => condition.MeetCondition())}");
            }
            // If the card has specific triggers, use those; otherwise, default to if its the faction's turn and a card hasn't been played this turn step
            return CardTriggers().Count > 0 && CardState.IsPlayed
                ? CardTriggers() 
                : _defaultPlayConditions; 
        }
    }
        

    protected virtual List<Condition> CardTriggers() => new();

    public List<CardStep> ExecutableCardSteps => CardSteps.Where(step => step.HasTagForAny(Tag.IsExecutable)).ToList();

    public CardLogic()
    {        
        CardSteps = OnActivate();
        EventBus.Instance.NewTurnStarted += OnNewTurnStarted;
    }    

    public void OnNewTurnStarted(int turnNumber)
    {
        if (IsStatus)
            CardSteps.ForEach(step => step.StepFinished = false);
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
