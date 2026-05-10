using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class DeckState : StateObject
{
    public FactionState FactionState { get; private set; }

    public Faction Faction => FactionState.FactionData.Faction;

    public string FactionLabel => Faction.ToString(); // Ensure Enum.Faction.GetKeys() returns a string list

    public List<int> AllCardIds =>
        DeckCardIds.Concat(HandCardIds)
                   .Concat(DiscardedCardIds)
                   .Concat(ResponseCardIds)
                   .Concat(StatusCardIds).ToList();

    public List<int> DeckCardIds = new();
    public List<CardState> DeckCardStates => CardState.ForIds(DeckCardIds);

    public List<int> HandCardIds = new();
    public List<CardState> HandCardStates => CardState.ForIds(HandCardIds);

    public List<int> DiscardedCardIds = new();
    public List<CardState> DiscardedCardStates => CardState.ForIds(DiscardedCardIds);

    public List<int> ResponseCardIds = new();
    public List<CardState> ResponseCardStates => CardState.ForIds(ResponseCardIds);

    public List<int> StatusCardIds = new();
    public List<CardState> StatusCardStates => CardState.ForIds(StatusCardIds);

    public DeckState(FactionState factionState)
    {
        FactionState = factionState;
    }   

    public int DrawTopCard()
    {
        if (DeckCardIds.Count == 0)
        {
            DebugUtilities.PrintPeerError($"Deck is empty for: {FactionLabel}");
            return -1;
        }

        int topCardId = DeckCardIds[0];
        DeckCardIds.RemoveAt(0);
        HandCardIds.Add(topCardId);

        DebugUtilities.PrintPeer($"Drew Card ({FactionLabel}): {CardState.ForId(topCardId).CardData.UniqueName}");
        return topCardId;
    }

    public List<int> DiscardTopCards(int number)
    {
        int overdraw = Math.Max(number - DeckCardIds.Count, 0);
        if (overdraw < 0)
        {
            DebugUtilities.PrintPeerError($"Deck is empty for: {FactionLabel}");
        }
        int actualDraw = number - overdraw;
        
        List<int> discardedCardIds = new List<int>();
        for (int i = 0; i < actualDraw; i++)
        {
            int topCardId = DeckCardIds[0];
            discardedCardIds.Add(topCardId);
            DeckCardIds.RemoveAt(0);
            
        }   
        DiscardedCardIds.AddRange(discardedCardIds);
        return discardedCardIds;     
    }

    public bool HasCardForName(string cardName)
    {
        return DeckCardStates.Any(card => card.CardData.UniqueName == cardName);
    }

    public int DrawCardByName(string cardName)
    {
        int index = DeckCardStates.FindIndex(card => card.CardData.UniqueName == cardName);
        if (index == -1)
            return -1;

        int cardId = DeckCardIds[index];
        DeckCardIds.RemoveAt(index);
        HandCardIds.Add(cardId);

        DebugUtilities.PrintPeerError($"Drew Card by name ({FactionLabel}): {CardState.ForId(cardId).CardData.Label}");
        return cardId;
    }

    public List<int> DrawCards(int number)
    {
        var response = new List<int>();
        for (int i = 0; i < number; i++)
        {
            int cardId = DrawTopCard();
            if (cardId > 0)
                response.Add(cardId);
        }
        return response;
    }    

    public void DiscardCardAtHandIndex(int index)
    {
        if (index >= HandCardIds.Count)
            return;

        DiscardCard(HandCardIds[index]);
    }

    public void PlayCard(int cardId)
    {
        if (!HandCardIds.Contains(cardId) && !DeckCardIds.Contains(cardId)){
            DebugUtilities.PrintPeerError($"Cannot play card that is not in hand or deck: {cardId}");
            return;
        }
        HandCardIds.Remove(cardId);            
        DeckCardIds.Remove(cardId);            

        CardState cardState = CardState.ForId(cardId);
        
        // Only discard event cards - Status and Response cards are moved to their respective lists
        // by their own PlayCardSteps logic (StatusCardLogic/ResponseCardLogic)
        if (!cardState.CardLogic.IsReaction)
        {
            DiscardCard(cardId);
        }
    }


    public void DiscardCard(int cardId)
    {
        if (HandCardIds.Contains(cardId))
        {
            HandCardIds.Remove(cardId);
        }
        else if (DeckCardIds.Contains(cardId))
        {
            DeckCardIds.Remove(cardId);
        }
        else if (StatusCardIds.Contains(cardId))
        {
            StatusCardIds.Remove(cardId);
        }
        else if (ResponseCardIds.Contains(cardId))
        {
            ResponseCardIds.Remove(cardId);
        }
        DiscardedCardIds.Add(cardId);
    }

    public void DebugHand()
    {
        DebugUtilities.PrintPeer($"Player {FactionLabel} has the following cards in hand:");
        foreach (var card in HandCardStates)
        {
            DebugUtilities.PrintPeer($"{HandCardStates.IndexOf(card)}. {card.CardData.UniqueName}");
        }
    }

    public void DebugStatusCards()
    {
        DebugUtilities.PrintPeer($"Player {FactionLabel} has the following status cards:");
        foreach (var card in StatusCardStates)
        {
            DebugUtilities.PrintPeer($"{StatusCardStates.IndexOf(card)}. {card.CardData.UniqueName}");
        }
    }

    public void ShuffleDeck()
    {        
        DeckCardIds.Shuffle();
    }

    public static DeckState ForFaction(Faction faction)
    {
        return GameSession.FactionStates[faction].DeckState;
    }
}
