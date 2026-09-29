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

    // The piles are the faction's board data; this class is the operations on them.
    public List<int> DeckCardIds { get => FactionState.Record.Deck; set => FactionState.Record.Deck = value; }
    public List<int> HandCardIds { get => FactionState.Record.Hand; set => FactionState.Record.Hand = value; }
    public List<int> DiscardedCardIds { get => FactionState.Record.Discarded; set => FactionState.Record.Discarded = value; }
    public List<int> ResponseCardIds { get => FactionState.Record.Response; set => FactionState.Record.Response = value; }
    public List<int> StatusCardIds { get => FactionState.Record.Status; set => FactionState.Record.Status = value; }

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

    // The pile operations live on BoardState so a fork moves its cards by the same code; these move
    // the live board's. See BoardState.Mutations for what each one guarantees.
    public int DrawTopCard() => BoardState.Live.DrawTopCard(Faction);
    public List<int> DiscardTopCards(int number) => BoardState.Live.DiscardTopCards(Faction, number);

    public bool HasCardForName(string cardName)
    {
        return DeckCardStates.Any(card => card.CardData.UniqueName == cardName);
    }

    public int DrawCardByName(string cardName) => BoardState.Live.DrawCardByName(Faction, cardName);
    public int FindDiscardableCardByName(string cardName) => BoardState.Live.FindDiscardableCardByName(Faction, cardName);
    public int DiscardCardByName(string cardName) => BoardState.Live.DiscardCardByName(Faction, cardName);
    public List<int> DrawCards(int number) => BoardState.Live.DrawCards(Faction, number);
    public void DiscardHandCards(List<int> cardIds) => BoardState.Live.DiscardHandCards(Faction, cardIds);

    public void DiscardCardAtHandIndex(int index)
    {
        if (index >= HandCardIds.Count)
            return;

        DiscardCard(HandCardIds[index]);
    }

    public void PlayCard(int cardId) => BoardState.Live.PlayCard(Faction, cardId);
    public void DiscardCard(int cardId) => BoardState.Live.DiscardCard(Faction, cardId);
    public bool RemoveCardFromAnyPile(int cardId) => BoardState.Live.RemoveCardFromAnyPile(Faction, cardId);

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
    /// Shuffle the draw deck and return the resulting order. <paramref name="authoritativeOrder"/> is
    /// the host/client split: the host shuffles and puts the order on the wire, the client applies it
    /// verbatim, because ComputeHash covers deck counts and not order.
    /// </summary>
    public List<int> ShuffleDeck(List<int> authoritativeOrder = null) => BoardState.Live.ShuffleDeck(Faction, authoritativeOrder);

    public static DeckState ForFaction(Faction faction)
    {
        return FactionState.ForEnum(faction).DeckState;
    }
}
