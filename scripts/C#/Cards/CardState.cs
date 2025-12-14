using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
public partial class CardState : Object
{
    public int Id { get; set; }
    public CardData CardData { get; set; }
    public Faction Faction { get; set; }

    public string CardName => CardData != null ? CardData.Label : string.Empty;

    public ChangeEvent TriggeredByChangeEvent { get; set; }

    private CardLogic cardLogic;
    public CardLogic CardLogic
    {
        get
        {
            if (cardLogic == null){
                cardLogic = GetCardLogicClass();
            }
            return cardLogic;
        }
    }

    // Constructor
    public CardState(CardData cardData)
    {
        CardData = cardData;
    }
    
    public bool CanPlayCard()
    {
        if (CardLogic != null)
            return CardLogic.CanPlayCard();
        return false;
    }

    private CardLogic GetCardLogicClass()
    {
        Type cardType = Type.GetType(CardData.ExecutionClass);
        if(cardType != null) {            
            CardLogic cardLogic = (CardLogic)Activator.CreateInstance(cardType);
            cardLogic.CardState = this;
            return cardLogic;
        }
        DebugUtilities.PrintPeerError($"Could not find card logic class for: {CardData.ExecutionClass}");
        return null;
    }

    // Static helpers
    public static CardState ForId(int cardId)
    {
        return GameSession.Instance.GameState.CardStatesById.TryGetValue(cardId, out var state) ? state : null;
    }

    // Static helpers
    public static List<CardState> ForIds(List<int> cardIds)
    {        
        return GameSession.Instance.GameState.CardStatesById.Values.ToList().Where(cardState => cardIds.Contains(cardState.Id)).ToList();
    }

    public static CardState ForName(string cardName)
    {
        return GameSession.Instance.GameState.CardStatesByName.TryGetValue(cardName, out var state) ? state : null;
    }
    public static List<CardState> ForNames(List<string> cardNames)
    {
        return cardNames.Map(c => GameSession.Instance.GameState.CardStatesByName.TryGetValue(c, out var state) ? state : null);
    } 
}