using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
public partial class CardState : StateObject
{
    [JsonIgnore] public CardData CardData { get; set; }
    public Faction Faction { get; set; }

    public string CardName => CardData != null ? CardData.Label : string.Empty;
    public ChangeEvent TriggeredByChangeEvent { get; set; }

    [JsonIgnore] public CardLogic CardLogic { get; set; } = null;

    public List<int> PlayedInTurn { get; set; } = new();
    public List<int> ActivatedInTurns { get; set; } = new();

    // Constructor
    public CardState(CardData cardData)
    {
        CardData = cardData;
        this.CardLogic = InitiateCardLogicClass();
    }
    
    public bool CanPlayCard => CardLogic?.IsPlayable ?? false;

    public CardLogic InitiateCardLogicClass()
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
    public static CardState ForId(int cardId) => GameSession.Current.GameState.CardStatesById.TryGetValue(cardId, out var state) ? state : null;
    // Static helpers
    public static List<CardState> ForIds(List<int> cardIds) => GameSession.Current.GameState.CardStatesById.Values.ToList().Where(cardState => cardIds.Contains(cardState.Id)).ToList();

    public static CardState ForName(string cardName) => GameSession.Current.GameState.CardStatesByName.Keys.Contains(cardName) ? GameSession.Current.GameState.CardStatesByName[cardName] : GameSession.Current.GameState.CardStatesByName.ToList().Find(kv => kv.Key.StartsWith(cardName)).Value;
    public static CardState ForNumber(int cardNumber)
    {
        if (!StaticGameData.CardDataByNumber.TryGetValue(cardNumber, out CardData cardData))
            return null;
        return ForName(cardData.UniqueName);
    }
    

    public static Dictionary<int, CardState> All => GameSession.Current.GameState.CardStatesById;
    public static Dictionary<int, CardState> AllForFaction(Faction faction) => 
        GameSession.Current.GameState.CardStatesById.Values
            .Where(cs => cs.Faction == faction)
            .ToDictionary(cs => cs.Id, cs => cs);
}