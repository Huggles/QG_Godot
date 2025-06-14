using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public abstract partial class CardLogic : GodotObject
{
    public CardState CardState;
    public CardData CardData => CardState.CardData;
    public Faction Faction => CardState.Faction;
    public FactionData FactionData => StaticGameData.FactionDataMap.ContainsKey(Faction) ? StaticGameData.FactionDataMap[Faction] : null;

    public List<int> ActivatedInTurns = new();
    public List<int> CompletedParts = new();

    public bool IsPlayed;
    public bool IsActivatedOnce => ActivatedInTurns.Count > 0;
    public bool IsActivatedThisTurn => ActivatedInTurns.Contains(GameSession.Instance.GameFlow.GameTurn);
    public bool IsPubliclyVisible => IsPlayed || (CardData.Type == "RESPONSE" && IsActivatedOnce);

    protected int PlayStep = 0;
    protected int ReactStep = 0;

    [Signal] public delegate void CardFinishedEventHandler();
    [Signal] public delegate void CardStepFinishedEventHandler();

    public virtual List<Action> OnPlaySteps() { return new() { InitialPlayStep }; }
    public virtual List<Action> OnReactSteps() { return new() { InitialReactStep }; }

    public abstract void InitialPlayStep();
    public virtual void InitialReactStep() { }

    public int NumberOfPlaySteps
    {
        get { return OnPlaySteps().Count; }
    }
    public int NumberOfReactSteps
    {
        get { return OnReactSteps().Count; }
    }

    public virtual bool CanPlayCard()
    {
        return false;
    }

    public virtual bool CanReactTo(ChangeEvent changeEvent)
    {
        return false;
    }

    public void PlayCard()
    {
        if (CanPlayCard())
        {
            string message = PlayActionGuidance();
            PlayerActionLabel.ShowText(message, -1, Faction);
            OnPlaySteps()[PlayStep].Invoke();
            PlayStep += 1;
        }
        else
        {
            DebugUtilities.PrintPeerError($"Cannot play card: {CardData.UniqueName}");
            Task.Delay(100);
        }
    }

    public async Task ReactTo(ChangeEvent changeEvent)
    {
        if (CanReactTo(changeEvent))
        {
            string message = ActivateActionGuidance();
            PlayerActionLabel.ShowText(message, -1, Faction);
            OnReactSteps()[PlayStep].Invoke();
            PlayStep += 1;
        }
        else
        {
            DebugUtilities.PrintPeerError($"Cannot activate card: {CardData.UniqueName}");
            await Task.Delay(100);
        }
    }

    protected virtual string PlayActionGuidance() =>
        $"Play {GetType().Name}";

    protected virtual string ActivateActionGuidance() =>
        $"Activate {GetType().Name}";

    public T BuildChangeEvent<T>(T changeEvent) where T : ChangeEvent
    {        
        changeEvent.SourceCardId = this.CardState.Id;
        return changeEvent;
    }


    

    

}
