using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public partial class DeckState : StateObject
{
    [JsonIgnore] public FactionState FactionState { get; private set; }

    public Faction Faction => FactionState.FactionData.Faction;

    public string FactionLabel => Faction.ToString(); // Ensure Enum.Faction.GetKeys() returns a string list

    public List<int> AllCardIds =>
        DeckCardIds.Concat(HandCardIds)
                   .Concat(DiscardedCardIds)
                   .Concat(ResponseCardIds)
                   .Concat(StatusCardIds).ToList();

    public List<int> DeckCardIds {get; set;} = new();
    public List<int> HandCardIds {get; set;} = new();
    public List<int> DiscardedCardIds {get; set;} = new();
    public List<int> ResponseCardIds {get; set;} = new();
    public List<int> StatusCardIds {get; set;} = new();

    [JsonIgnore] public List<CardState> DeckCardStates => CardState.ForIds(DeckCardIds);
    [JsonIgnore] public List<CardState> HandCardStates => CardState.ForIds(HandCardIds);
    [JsonIgnore] public List<CardState> DiscardedCardStates => CardState.ForIds(DiscardedCardIds);
    [JsonIgnore] public List<CardState> ResponseCardStates => CardState.ForIds(ResponseCardIds);
    [JsonIgnore] public List<CardState> StatusCardStates => CardState.ForIds(StatusCardIds);
    public List<int> ActivatableCardIds => CardState.AllForFaction(Faction).Values.ToList().Where(cs => cs.HasTag(Tag.IsActivatable, Faction)).Select(cs => cs.Id).ToList();        
    
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
        return topCardId;
    }

    public List<int> DiscardTopCards(int number)
    {
        int overdraw = Math.Max(number - DeckCardIds.Count, 0);
        if (overdraw > 0)
        {
            DebugUtilities.PrintPeerError($"Deck has only {DeckCardIds.Count} of {number} cards to discard for: {FactionLabel}");
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

    public void DiscardHandCards(List<int> cardIds)
    {
        foreach (int cardId in cardIds)
        {
            DiscardCard(cardId);
        }
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

        if (cardState.CardData.CardType == CardType.STATUS)
        {
            StatusCardIds.Add(cardId);
            if (cardState.CardLogic is IModifier modifier)
                ModifierRegistry.Register(modifier);
        }
        else if (cardState.CardData.CardType == CardType.RESPONSE)
            ResponseCardIds.Add(cardId);
        else
            DiscardCard(cardId);
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
            CardState statusCard = CardState.ForId(cardId);
            if (statusCard.CardLogic is IModifier modifier)
                ModifierRegistry.Unregister(modifier);
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
        return FactionState.ForEnum(faction).DeckState;
    }
}
