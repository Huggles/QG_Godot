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

    // IsPlayed is computed based on the card's location in DeckState
    public bool IsPlayed
    {
        get
        {
            DeckState deckState = DeckState.ForFaction(Faction);
            
            if (CardData.Type == "STATUS")
            {
                return deckState.StatusCardIds.Contains(CardState.Id);
            }
            else if (CardData.Type == "RESPONSE")
            {
                return deckState.ResponseCardIds.Contains(CardState.Id);
            }
            else
            {
                // Event cards are played if they're in the discard pile or currently in the card pool
                return deckState.DiscardedCardIds.Contains(CardState.Id) || CardPlayPool.CardPoolMap.ContainsKey(CardState.Id);
            }
        }
    }
    
    public bool IsActivatedOnce => ActivatedInTurns.Count > 0;
    public bool IsActivatedThisTurn => ActivatedInTurns.Contains(GameFlow.Instance.GameTurn);
    public bool IsReaction => CardData.Type == "RESPONSE" || CardData.Type == "STATUS";
    public bool IsPubliclyVisible => IsPlayed || (CardData.Type == "RESPONSE" && IsActivatedOnce);
    public bool IsPlayFinished = false;
    public bool IsActivationFinished = false;
    public bool IsBlockReaction => CardTriggers().Any(triggerCondition => triggerCondition is Condition.IsBlockRequest);
    public int NextStepId
    {
        get
        {
            if (!IsPlayed)
            {
                return ExecutablePlaySteps[0].Id;
            }
            else if (IsReaction)
            {
                return ExecutableReactSteps[0].Id;
            }
            else
            {
                return -1;
            }            
        }
    }

    [Signal] public delegate void CardFinishedEventHandler();
    
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
        // Status and Response cards must be played before they can be activated
        if (IsReaction && !IsPlayed)
        {
            return false;
        }
        
        bool canActivate = !IsActivatedThisTurn && !IsActivationFinished && TriggerConditionsMet;
        if (CardData.Type == "STATUS" || CardData.Type == "RESPONSE")
        {
            DebugUtilities.PrintPeer($"CanBeActivated {CardData.UniqueName}: IsPlayed={IsPlayed}, IsActivatedThisTurn={IsActivatedThisTurn}, IsActivationFinished={IsActivationFinished}, TriggerConditionsMet={TriggerConditionsMet}, Result={canActivate}");
            if (CardTriggers().Count > 0)
            {
                foreach (var trigger in CardTriggers())
                {
                    DebugUtilities.PrintPeer($"  Trigger {trigger.GetType().Name}: {trigger.MeetCondition()}");
                }
            }
        }
        return canActivate;
    }
    private bool TriggerConditionsMet
    {
        get { return CardTriggers().Count > 0 && CardTriggers().All(condition=>condition.MeetCondition()); }
    }

    protected virtual List<Condition> CardTriggers()
    {
        return new();
    }
    public List<CardStep> ExecutablePlaySteps => PlayCardSteps.Where(playCardStep => !playCardStep.StepFinished && playCardStep.PrerequisiteStepFinished && playCardStep.MeetAllConditions).ToList();
    public List<CardStep> ExecutableReactSteps => ReactCardSteps.Where(reactCardStep => !reactCardStep.StepFinished && reactCardStep.PrerequisiteStepFinished && reactCardStep.MeetAllConditions).ToList();

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

    public async Task<ChangeEvent> PlayCard(int stepId)
    {
        string message = PlayActionGuidance();
        PlayerActionLabel.ShowText(message, -1, Faction);
        return await CardStepForId(stepId).Execute();
    }

    public async Task<ChangeEvent> React(int stepId)
    {
        string message = ActivateActionGuidance();
        PlayerActionLabel.ShowText(message, -1, Faction);        
        return await CardStepForId(stepId).Execute();        
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
