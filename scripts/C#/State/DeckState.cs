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

    /// <summary>Debug-log only (never on the wire — FactionStateDto does not carry it).</summary>
    public string FactionLabel => Faction.Label();

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

    /// <summary>
    /// Pull a named card out of the draw deck and into hand, wherever it is sitting in the deck.
    ///
    /// The search MUST run over DeckCardIds and not over DeckCardStates. DeckCardStates is
    /// CardState.ForIds(DeckCardIds), and ForIds filters the master CardStatesById dictionary — so it
    /// comes back in card-id order, not deck order. This used to be a FindIndex over DeckCardStates
    /// fed into DeckCardIds[index], which was only ever correct because ids were handed out in deck
    /// declaration order and nothing reordered the deck. ShuffleDecks now permutes DeckCardIds before
    /// the opening hands are dealt, so that index pointed at an unrelated card and returned it
    /// silently (the id is valid, so the -1 "not found" path never fired): scenario initialHandCards
    /// stayed in the deck and a random card went to hand instead.
    /// </summary>
    public int DrawCardByName(string cardName)
    {
        int index = DeckCardIds.FindIndex(id => CardState.ForId(id)?.CardData.UniqueName == cardName);
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
        {
            // A Response card goes onto the table face down, whether this is its first play or a
            // return after being recycled out of the discard pile. The guard above means a discarded
            // card can only get here by being recycled to hand or deck first, but the rule belongs at
            // the pile that owns it rather than depending on that.
            cardState.IsRevealed = false;
            ResponseCardIds.Add(cardId);
        }
        else
            DiscardCard(cardId);
    }


    /// <summary>
    /// This was a Contains/Remove chain over hand, deck, status and response that ended in an
    /// unconditional Add. Removing from every pile instead also covers the case the chain fell all the
    /// way through: a card already in DiscardedCardIds got appended a second time. That is a live path,
    /// not a hypothetical — see the IsDiscarded guard in ActivateReactionChangeEvent, which exists to
    /// keep multi-step Response cards away from it.
    /// </summary>
    public void DiscardCard(int cardId)
    {
        RemoveCardFromAnyPile(cardId);
        DiscardedCardIds.Add(cardId);
    }

    /// <summary>
    /// Take a card out of every pile it could be sitting in, and report whether any of them held it.
    ///
    /// A card id must appear in exactly one of the five pile lists. Nothing enforces that structurally
    /// — AllCardIds is a plain Concat, ComputeHash folds in the deck count and the hand/status/response
    /// id lists, and every hand/deck size condition counts a list — so a card in two piles reads as two
    /// cards everywhere. Any code that *moves* a card must therefore take it out of its current pile
    /// rather than assume which pile that is; RecycleCardChangeEvent assumed "discard pile" and
    /// duplicated the card whenever it was not there.
    ///
    /// A status card leaving the board takes its modifier registration with it — otherwise the modifier
    /// stays live with nothing in play behind it.
    /// </summary>
    public bool RemoveCardFromAnyPile(int cardId)
    {
        // Non-short-circuiting `|`: every pile gets cleared, not just the first one that matches.
        bool removed = HandCardIds.Remove(cardId)
                     | DeckCardIds.Remove(cardId)
                     | DiscardedCardIds.Remove(cardId)
                     | ResponseCardIds.Remove(cardId);

        if (StatusCardIds.Remove(cardId))
        {
            removed = true;
            CardState statusCard = CardState.ForId(cardId);
            // Persistent modifiers are meant to outlive their card — see IPersistentModifier.
            if (statusCard.CardLogic is IModifier modifier and not IPersistentModifier)
                ModifierRegistry.Unregister(modifier);
        }
        return removed;
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

    /// <summary>
    /// Shuffle the draw deck and return the resulting order.
    ///
    /// This used to be <c>DeckCardIds.Shuffle();</c>, which was a silent no-op: the Shuffle extension
    /// returns a NEW list and does not mutate, so the result was discarded and the deck was left
    /// untouched. Its only caller is RecycleCardChangeEvent's ShuffleIntoDeck branch, which therefore
    /// appended to the bottom of the deck and called it shuffled.
    ///
    /// <paramref name="authoritativeOrder"/> is the host/client split. That branch runs on BOTH peers
    /// (it rides the replicated ChangeEvent stream), and ComputeHash covers deck *counts* but not
    /// order — so if each peer shuffled for itself they would diverge with nothing to detect it until
    /// a mismatched hand surfaced several draws later. The host shuffles and puts the resulting order
    /// on the wire; the client applies it verbatim. Same pattern as ReorderDeckChangeEvent.
    /// </summary>
    public List<int> ShuffleDeck(List<int> authoritativeOrder = null)
    {
        if (authoritativeOrder != null)
        {
            // Mutated in place, never reassigned: DeckState lists are live objects other code holds.
            DeckCardIds.Clear();
            DeckCardIds.AddRange(authoritativeOrder);
        }
        else
        {
            GameRandom.Shuffle(DeckCardIds);
        }
        return new List<int>(DeckCardIds);
    }

    public static DeckState ForFaction(Faction faction)
    {
        return FactionState.ForEnum(faction).DeckState;
    }
}
