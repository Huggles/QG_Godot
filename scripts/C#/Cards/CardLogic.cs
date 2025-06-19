using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
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

    public virtual bool CanPlayCard()
    {
        return PlayCardSteps.Where(playStep => !playStep.StepFinished).ToList().Count > 0;
    }
    public virtual bool CanReactTo(ChangeEvent changeEvent)
    {
        return !IsActivatedThisTurn && ReactCardSteps.Where(playStep => !playStep.StepFinished).ToList().Count > 0;
    }
    public List<CardStep> ExecutablePlaySteps()
    {
        return PlayCardSteps.Where(playCardStep => !playCardStep.StepFinished && playCardStep.PrerequisiteStepFinished).ToList();
    }
    public List<CardStep> ExecutableReactSteps()
    {
        return ReactCardSteps.Where(playCardStep => !playCardStep.StepFinished && playCardStep.PrerequisiteStepFinished).ToList();
    }

    public CardStep CardStepForId(int id)
    {
        CardStep cardStep = PlayCardSteps.Find(step=>step.Id == id);
        if (cardStep == null) {
            cardStep = ReactCardSteps.Find(step=>step.Id == id);
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
        EventBus.Instance.CardPlayPoolFinished += () =>
        {
            ReactCardSteps.ForEach(reactCardStep => reactCardStep.StepFinished = false);
        };
    }
    

    public void PlayCard(int stepId)
    {
        if (CanPlayCard())
        {
            string message = PlayActionGuidance();
            PlayerActionLabel.ShowText(message, -1, Faction);
            CardStepForId(stepId).Execute();
        }
        else
        {
            DebugUtilities.PrintPeerError($"Cannot play card: {CardData.UniqueName}");
            Task.Delay(100);
        }
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
