using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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

    public List<CardActivationOption> ActivatableCardsForGCE(ChangeEvent gce, bool before = false)
    {
        var ac = ActivatableCardsForGCEInCards(StatusCardStates, gce, before);
        ac.AddRange(ActivatableCardsForGCEInCards(ResponseCardStates, gce, before));
        return ac;
    }

    public List<CardActivationOption> ActivatableCards(bool before = false)
    {
        var ac = ActivatableStatusCards(before);
        ac.AddRange(ActivatableResponseCards(before));
        return ac;
    }

    private List<CardActivationOption> ActivatableStatusCards(bool before = false)
    {
        var options = new List<CardActivationOption>();
        // foreach (var gce in GameState.CardPlayHandler.ChangeEvents)
        // {
        //     options.AddRange(ActivatableCardsForGCEInCards(StatusCardStates, gce, before));
        // }
        return options;
    }

    private List<CardActivationOption> ActivatableResponseCards(bool before = false)
    {
        var options = new List<CardActivationOption>();
        // foreach (var gce in GameState.CardPlayHandler.ChangeEvents)
        // {
        //     options.AddRange(ActivatableCardsForGCEInCards(ResponseCardStates, gce, before));
        // }
        return options;
    }

    private List<CardActivationOption> ActivatableCardsForGCEInCards(List<CardState> cards, ChangeEvent gce, bool before = false)
    {
        var options = new List<CardActivationOption>();
        foreach (var card in cards)
        {
            bool canActivate = before
                ? card.CardExecutionClass.CanActivateBefore(gce)
                : card.CardExecutionClass.CanActivateAction(gce);

            if (canActivate)
            {
                var option = new CardActivationOption(card.Id, "response");
                options.Add(option);
            }
        }
        return options;
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

    public void PlayCardAtHandIndex(int index)
    {
        if (index >= HandCardIds.Count)
            return;

        PlayCard(HandCardIds[index]);
    }

    public void PlayCard(int cardId)
    {
        var cardState = CardState.ForId(cardId);
        if (!cardState.CanPlayCard())
        {
            DebugUtilities.PrintPeerError($"Cannot play card: {cardState.CardData.Label}");
            return;
        }

        if (HandCardIds.Contains(cardId))
            PlayCardFromHand(cardId);
        else if (DiscardedCardIds.Contains(cardId))
            PlayCardFromDiscard(cardId);
        else if (DeckCardIds.Contains(cardId))
            PlayCardFromDeck(cardId);
    }

    public void ActivateCard(CardActivationOption option)
    {
        if (StatusCardIds.Contains(option.CardId))
            ActivateStatusCard(option);
        else if (ResponseCardIds.Contains(option.CardId))
            ActivateResponseCard(option);
    }

    public void ActivateStatusCard(CardActivationOption option)
    {
        if (!StatusCardIds.Contains(option.CardId)) return;

        var card = CardState.ForId(option.CardId);
        card.CardExecutionClass.ActivateCard(ChangeEvent.ForId(option.ChangeEventId));
        
    }

    public void ActivateResponseCard(CardActivationOption option)
    {
        if (!ResponseCardIds.Contains(option.CardId)) return;

        var card = CardState.ForId(option.CardId);
        card.CardExecutionClass.ActivateCard(ChangeEvent.ForId(option.ChangeEventId));
        
    }

    public CardState PlayCardFromDeck(int cardId)
    {
        if (!DeckCardIds.Contains(cardId)) return null;

        DeckCardIds.Remove(cardId);
        DiscardedCardIds.Add(cardId);
        return CardState.ForId(cardId);
    }

    public CardState PlayCardFromDiscard(int cardId)
    {
        return DiscardedCardIds.Contains(cardId) ? CardState.ForId(cardId) : null;
    }

    public CardState PlayCardFromHand(int cardId)
    {
        if (!HandCardIds.Contains(cardId)) return null;

        var card = CardState.ForId(cardId);
        if (!card.CanPlayCard())
        {
            DebugUtilities.PrintPeerError($"Card not playable: {card.CardData.Label}");
            return null;
        }

        HandCardIds.Remove(cardId);
        DiscardedCardIds.Add(cardId);
        return card;
    }

    public void PlayCardByName(string cardName)
    {
        foreach (var card in CardState.ForIds(AllCardIds))
        {
            if (card.CardData.UniqueName == cardName)
            {
                PlayCard(card.Id);
                return;
            }
        }
    }

    public void DiscardCardAtHandIndex(int index)
    {
        if (index >= HandCardIds.Count)
            return;

        DiscardCard(HandCardIds[index]);
    }

    public void DiscardCard(int cardId)
    {
        if (!HandCardIds.Contains(cardId)) return;

        HandCardIds.Remove(cardId);
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
        return GameSession.Instance.GameState.FactionStateForEnum(faction).DeckState;
    }
}
