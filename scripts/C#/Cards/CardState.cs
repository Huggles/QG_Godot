using Godot;
using System;
using System.Collections.Generic;
public partial class CardState : Object
{
    public int Id { get; set; }
    public CardData CardData { get; set; }
    public Faction Faction { get; set; }

    public string CardName => CardData != null ? CardData.Label : string.Empty;

    public ChangeEvent TriggeredByChangeEvent { get; set; }

    private CardLogic cardExecutionClass;

    public CardLogic CardExecutionClass
    {
        get
        {
            if (cardExecutionClass == null){
                cardExecutionClass = GetCardLogicClass();
            }
            return cardExecutionClass;
        }
    }

    // Constructor
    public CardState(CardData cardData)
    {
        CardData = cardData;
    }

    public bool CanPlayCard()
    {
        if (CardExecutionClass != null)
            return CardExecutionClass.CanPlayCard();
        return false;
    }

    public void PlayCard()
    {
        DebugUtilities.PrintPeer("play_card");
        CardExecutionClass.PlayCard();
    }

    private CardLogic GetCardLogicClass()
    {
        if (DataUtilities.ClassMap.ContainsKey(CardData.ExecutionClass))
        {
            var classPath = DataUtilities.ClassMap[CardData.ExecutionClass].Path;
            var script = GD.Load<Script>(classPath);
            if (script != null)
            {
                CardLogic instance = new CardLogic(this);                
                return instance;
            }
        }

        DebugUtilities.PrintPeerError($"Could not find card logic class for: {CardData.Label}");
        return null;
    }

    // Static helpers
    public static CardState ForId(int cardId)
    {
        return GameSession.Instance.GameState.CardStatesById.TryGetValue(cardId, out var state) ? state : null;
    }

    public static List<CardState> ForIds(List<int> cardIds)
    {
        var response = new List<CardState>();
        foreach (var cardId in cardIds)
        {
            var state = ForId(cardId);
            if (state != null)
                response.Add(state);
        }
        return response;
    }

    public static CardState ForName(string cardName)
    {
        return GameSession.Instance.GameState.CardStatesByName.TryGetValue(cardName, out var state) ? state : null;
    }
}