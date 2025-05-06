using Godot;
using System;
using System.Collections.Generic;

public partial class CardLogic
{
    public int PeerId;
    public CardState CardState;

    public CardData CardData => CardState.CardData;

    public Faction Faction => CardState.Faction;

    public FactionData FactionData => StaticGameData.FactionDataMap.ContainsKey(Faction) ? StaticGameData.FactionDataMap[Faction] : null;

    public bool IsPlayed;

    public List<int> ActivatedInTurns = new();

    public bool IsActivatedOnce => ActivatedInTurns.Count > 0;

    public bool IsActivatedThisTurn => ActivatedInTurns.Contains(GameSession.Instance.GameFlow.GameTurn);

    public bool IsPubliclyVisible =>
        IsPlayed || (CardData.Type == "RESPONSE" && IsActivatedOnce);

    public List<int> CompletedParts = new();

    // public Texture2D CardFrontTexture
    // {
    //     get
    //     {
    //         switch (CardData.Type)
    //         {
    //             case "BUILD_ARMY": return GD.Load<Texture2D>(FactionData.CardFrontBuildArmyTexture);
    //             case "BUILD_NAVY": return GD.Load<Texture2D>(FactionData.CardFrontBuildNavyTexture);
    //             case "LAND_BATTLE": return GD.Load<Texture2D>(FactionData.CardFrontLandBattleTexture);
    //             case "SEA_BATTLE": return GD.Load<Texture2D>(FactionData.CardFrontSeaBattleTexture);
    //             case "STATUS": return GD.Load<Texture2D>(FactionData.CardFrontStatusTexture);
    //             case "RESPONSE": return GD.Load<Texture2D>(FactionData.CardFrontResponseTexture);
    //             case "EVENT": return GD.Load<Texture2D>(FactionData.CardFrontEventTexture);
    //             case "EW": return GD.Load<Texture2D>(FactionData.CardFrontEwTexture);
    //             default: return null;
    //         }
    //     }
    // }

    // public Texture2D CardBackTexture => GD.Load<Texture2D>(FactionData.CardBackTexture);

    public CardLogic(CardState cardState)
    {
        CardState = cardState;
    }

    protected int PartCounter = 1;

    public virtual bool CanPlayCard() => CanPlayCard(PartCounter);

    protected virtual bool CanPlayCard(int part) => true;

    public bool CanActivateAction(ChangeEvent changeEvent)
    {
        bool activatable = CanActivateActionImpl(changeEvent) && !IsActivatedThisTurn;
        DebugUtilities.PrintPeer($"{CardData.Label} is activatable: {activatable} for {changeEvent.SummaryText()}");
        return activatable;
    }

    protected virtual bool CanActivateActionImpl(ChangeEvent changeEvent) => false;

    public void PlayCard()
    {
        if (CanPlayCard(PartCounter))
        {
            string message = PlayActionGuidance(PartCounter);
            PlayerActionLabel.ShowText(message, -1, Faction);
            PlayCardImpl(PartCounter);
        }
        else
        {
            DebugUtilities.PrintPeerError($"Cannot execute card: {CardData.UniqueName}");
        }
    }

    public void ActivateCard(ChangeEvent changeEvent)
    {
        if (CanActivateAction(changeEvent))
        {
            EventBus.Emit("StatusCardActivationStarted", CardState.Id);            
            ActivatedInTurns.Add(GameSession.Instance.GameFlow.GameTurn);
            string message = ActivateActionGuidance(PartCounter);
            PlayerActionLabel.ShowText(message, -1, Faction);            
            ActivateActionImpl(changeEvent, PartCounter);
        }
        else
        {
            DebugUtilities.PrintPeerError($"Cannot activate card: {CardData.UniqueName}");
        }
    }

    protected virtual string PlayActionGuidance(int part) =>
        $"Play {GetType().Name}";

    protected virtual string ActivateActionGuidance(int part) =>
        $"Activate {GetType().Name}";

    protected virtual void PlayCardImpl(int part)
    {
        // Override this in derived class
    }

    protected virtual void ActivateActionImpl(ChangeEvent changeEvent, int part)
    {
        // Override this in derived class
    }

    public bool CanActivateBefore(ChangeEvent changeEvent) =>
        CanActivateBeforeImpl(changeEvent);

    protected virtual bool CanActivateBeforeImpl(ChangeEvent changeEvent) => false;

    public void ActivateBefore(ChangeEvent changeEvent) =>
        ActivateBeforeImpl(changeEvent);

    protected virtual void ActivateBeforeImpl(ChangeEvent changeEvent)
    {
        // Override if needed
    }

    protected virtual bool HasMultipleActions() => false;

    public bool HasNextAction() => HasNextActionImpl();

    protected virtual bool HasNextActionImpl() => false;

    public void PlayNextAction()
    {
        PartCounter += 1;
        PlayCard();
    }

    public void ActivateNextAction(ChangeEvent changeEvent)
    {
        PartCounter += 1;
        ActivateCard(changeEvent);
    }

    public void CardPlayFinished()
    {
        GD.Print($"Finished play: {GetType().Name}");
        EventBus.Emit("CardPlayCompleted", CardState.Id);
    }

    public void CardActivationFinished()
    {
        GD.Print($"Finished activation: {GetType().Name}");
        CompletedParts.Clear();
        EventBus.Emit("StatusCardActivationCompleted", CardState.Id);
    }
}
