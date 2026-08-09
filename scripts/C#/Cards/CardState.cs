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

    public virtual bool IsPlayed => this.HasTag(Tag.IsPlayed, Faction);

    /// <summary>
    /// In its owner's discard pile. Deliberately not derived from Tag.IsPlayed — discarded cards keep
    /// that tag (see GameStateCalculator.CalculatePlayedCardsForFaction), so "played" and "discarded"
    /// cannot be told apart by tag alone.
    /// </summary>
    public virtual bool IsDiscarded => DeckState.ForFaction(Faction).DiscardedCardIds.Contains(Id);

    /// <summary>
    /// The card face CardScene renders. Virtual so a card that is not a faction card — see
    /// BulletinCardState — can supply its own art instead of a per-faction frame. Only ever read by the
    /// UI, so a headless server (which never loads card textures) never evaluates it.
    /// </summary>
    [JsonIgnore]
    public virtual Texture2D FrontTexture =>
        FactionState.ForEnum(Faction).FactionData.CardFrontTextures[CardData.CardType];

    // Constructor
    public CardState(CardData cardData)
    {
        CardData = cardData;
        this.CardLogic = InitiateCardLogicClass();
    }

    /// <summary>
    /// Takes a pre-built CardLogic instead of reflecting one from CardData.ExecutionClass. Used by
    /// BulletinCardState: an activatable mutator's Label/Text live on its CardLogic subclass, so the
    /// logic must exist before the synthetic CardData can be built. Callers are responsible for
    /// setting CardLogic.CardState and CardLogic.CardSteps, which InitiateCardLogicClass does itself.
    /// </summary>
    protected CardState(CardData cardData, CardLogic cardLogic)
    {
        CardData = cardData;
        this.CardLogic = cardLogic;
    }

    public CardLogic InitiateCardLogicClass()
    {
        Type cardType = Type.GetType(CardData.ExecutionClass);
        if(cardType != null) {            
            CardLogic cardLogic = (CardLogic)Activator.CreateInstance(cardType);
            cardLogic.CardState = this;
            cardLogic.CardSteps = cardLogic.OnActivate();
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