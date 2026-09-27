using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A hypothetical board: the live one plus the effects of ChangeEvents that have NOT been applied.
/// Each event says what it would do through <see cref="ChangeEvent.Project"/>, so a bot can ask "what
/// does the board look like after this card" without touching the game.
///
/// Copy-on-write over live state: reads fall through to CountryState / UnitState / DeckState / FactionState
/// wherever nothing has been overridden, and nothing here ever writes to them or draws from any RNG.
/// It models occupancy, unit locations, the unit pool, score and hand/deck card counts — not tags,
/// supply or reactions. An event it cannot model marks the whole projection <see cref="IsUnknown"/>.
/// </summary>
public sealed class BoardProjection
{
    private readonly Dictionary<int, Dictionary<Faction, int>> _units = new();
    private readonly Dictionary<int, int> _unitCountry = new();
    // Absolute values, copied from the live board on first write — like the occupancy above — so the
    // projection stays what it was even if the live board moves on after it was made.
    private readonly Dictionary<(Faction, UnitType), int> _pool = new();
    private readonly Dictionary<Faction, int> _score = new();
    private readonly Dictionary<Faction, int> _hand = new();
    private readonly Dictionary<Faction, int> _deck = new();
    private readonly Dictionary<int, (Faction Faction, UnitType Type)> _projectedUnits = new();
    private readonly Dictionary<int, CardPile> _cardPile = new();
    private int _nextProjectedUnitId = -2;

    /// <summary>True once any projected event could not be modelled; every read is then a guess.</summary>
    public bool IsUnknown => UnknownReason != null;

    /// <summary>Why the projection went unknown — the first reason only.</summary>
    public string UnknownReason { get; private set; }

    private BoardProjection() { }

    /// <summary>A projection of the live board with nothing applied yet.</summary>
    public static BoardProjection FromLive() => new();

    /// <summary>An independent copy, so candidates can be tried side by side from the same start.</summary>
    public BoardProjection Fork()
    {
        BoardProjection copy = new() { UnknownReason = UnknownReason, _nextProjectedUnitId = _nextProjectedUnitId };
        foreach (var (countryId, units) in _units) copy._units[countryId] = new Dictionary<Faction, int>(units);
        foreach (var kv in _unitCountry) copy._unitCountry[kv.Key] = kv.Value;
        foreach (var kv in _pool) copy._pool[kv.Key] = kv.Value;
        foreach (var kv in _score) copy._score[kv.Key] = kv.Value;
        foreach (var kv in _hand) copy._hand[kv.Key] = kv.Value;
        foreach (var kv in _deck) copy._deck[kv.Key] = kv.Value;
        foreach (var kv in _projectedUnits) copy._projectedUnits[kv.Key] = kv.Value;
        foreach (var kv in _cardPile) copy._cardPile[kv.Key] = kv.Value;
        return copy;
    }

    /// <summary>Project one event. Returns false when it could not be modelled (the projection is then unknown).</summary>
    public bool Apply(ChangeEvent changeEvent)
    {
        if (changeEvent == null) return !IsUnknown;
        try { changeEvent.Project(this); }
        catch (Exception e) { MarkUnknown($"{changeEvent.GetType().Name} threw {e.GetType().Name}"); }
        return !IsUnknown;
    }

    // ── Reads ────────────────────────────────────────────────────────────────

    /// <summary>Who stands in a country: faction → unit id. Projected pieces have negative ids.</summary>
    public IReadOnlyDictionary<Faction, int> UnitsIn(int countryId)
        => _units.TryGetValue(countryId, out var units) ? units : CountryState.ForId(countryId).Units;

    /// <summary>Where a unit stands, or -1 when it is in the pool.</summary>
    public int CountryOf(int unitId)
        => _unitCountry.TryGetValue(unitId, out int countryId) ? countryId : UnitState.ForId(unitId).CountryId;

    public Faction FactionOf(int unitId)
        => _projectedUnits.TryGetValue(unitId, out var unit) ? unit.Faction : UnitState.ForId(unitId).Faction;

    public UnitType TypeOf(int unitId)
        => _projectedUnits.TryGetValue(unitId, out var unit) ? unit.Type : UnitState.ForId(unitId).Type;

    public int AvailableUnits(Faction faction, UnitType type)
        => _pool.TryGetValue((faction, type), out int n) ? n : UnitPool.AvailableUnitCount(faction, type);

    public int ScoreOf(Faction faction)
        => _score.TryGetValue(faction, out int n) ? n : FactionState.ForEnum(faction).Score;

    public int HandCount(Faction faction)
        => _hand.TryGetValue(faction, out int n) ? n : DeckState.ForFaction(faction).HandCardIds.Count;

    public int DeckCount(Faction faction)
        => _deck.TryGetValue(faction, out int n) ? n : DeckState.ForFaction(faction).DeckCardIds.Count;

    /// <summary>Which pile a card is in. Discards chosen by a player are counted but not tracked per card.</summary>
    public CardPile PileOf(int cardId)
    {
        if (_cardPile.TryGetValue(cardId, out CardPile pile)) return pile;
        CardState card = CardState.ForId(cardId);
        DeckState deck = card == null ? null : DeckState.ForFaction(card.Faction);
        if (deck == null) return CardPile.None;
        if (deck.HandCardIds.Contains(cardId)) return CardPile.Hand;
        if (deck.DeckCardIds.Contains(cardId)) return CardPile.Deck;
        if (deck.DiscardedCardIds.Contains(cardId)) return CardPile.Discard;
        if (deck.StatusCardIds.Contains(cardId) || deck.ResponseCardIds.Contains(cardId)) return CardPile.Table;
        return CardPile.None;
    }

    /// <summary>Countries whose occupancy differs from the live board.</summary>
    public IEnumerable<int> ChangedCountryIds => _units.Keys;

    // ── Writes, called by ChangeEvent.Project ────────────────────────────────

    /// <summary>
    /// Mirror of GameAPI.DeployUnitToCountry: a faction already there rebuilds in place (no change);
    /// otherwise a pool piece moves in. An empty pool or a full country is not modelled — the real game
    /// would prompt for a recall or refuse the deploy.
    /// </summary>
    public void Deploy(Faction faction, int countryId, UnitType type)
    {
        IReadOnlyDictionary<Faction, int> current = UnitsIn(countryId);
        if (current.ContainsKey(faction)) return;
        if (current.Count >= 3) { MarkUnknown($"deploy into full country {countryId}"); return; }
        if (AvailableUnits(faction, type) <= 0) { MarkUnknown($"{faction} has no {type} in the pool"); return; }

        int unitId = _nextProjectedUnitId--;
        _projectedUnits[unitId] = (faction, type);
        _pool[(faction, type)] = AvailableUnits(faction, type) - 1;
        _unitCountry[unitId] = countryId;
        Writable(countryId)[faction] = unitId;
    }

    /// <summary>Mirror of GameAPI.RemoveUnitFromCountry: the unit leaves its country and returns to the pool.</summary>
    public void Remove(int unitId)
    {
        int countryId = CountryOf(unitId);
        if (countryId < 0) { MarkUnknown($"remove unit {unitId}, which is not on the board"); return; }

        Faction faction = FactionOf(unitId);
        UnitType type = TypeOf(unitId);
        Writable(countryId).Remove(faction);
        _unitCountry[unitId] = -1;
        _pool[(faction, type)] = AvailableUnits(faction, type) + 1;
    }

    public void AddScore(Faction faction, int points)
        => _score[faction] = ScoreOf(faction) + points;

    /// <summary>The count leaves the hand; which cards is the player's choice and is not modelled.</summary>
    public void DiscardFromHand(Faction faction, int count)
        => _hand[faction] = HandCount(faction) - Math.Min(count, HandCount(faction));

    /// <summary>Mirror of DeckState.DiscardTopCards: each card the deck cannot pay costs 1 VP instead.</summary>
    public void DiscardFromDeck(Faction faction, int count)
    {
        int paid = Math.Min(count, DeckCount(faction));
        _deck[faction] = DeckCount(faction) - paid;
        if (count > paid) AddScore(faction, -(count - paid));
    }

    /// <summary>Move a card between piles, keeping the owner's hand and deck counts in step.</summary>
    public void MoveCard(int cardId, CardPile to)
    {
        Faction owner = CardState.ForId(cardId).Faction;
        CardPile from = PileOf(cardId);
        if (from == CardPile.Hand) _hand[owner] = HandCount(owner) - 1;
        if (from == CardPile.Deck) _deck[owner] = DeckCount(owner) - 1;
        if (to == CardPile.Hand) _hand[owner] = HandCount(owner) + 1;
        if (to == CardPile.Deck) _deck[owner] = DeckCount(owner) + 1;
        _cardPile[cardId] = to;
    }

    /// <summary>Mirror of DeckState.PlayCard: from hand or deck to the table (Status, Response) or the discard.</summary>
    public void PlayCard(int cardId)
    {
        CardPile from = PileOf(cardId);
        if (from != CardPile.Hand && from != CardPile.Deck) { MarkUnknown($"play card {cardId}, which is not in hand or deck"); return; }
        CardType type = CardState.ForId(cardId).CardData.CardType;
        MoveCard(cardId, type is CardType.STATUS or CardType.RESPONSE ? CardPile.Table : CardPile.Discard);
    }

    /// <summary>Give up on modelling. Only the first reason is kept.</summary>
    public void MarkUnknown(string reason) => UnknownReason ??= reason;

    private Dictionary<Faction, int> Writable(int countryId)
    {
        if (!_units.TryGetValue(countryId, out var units))
        {
            units = new Dictionary<Faction, int>(CountryState.ForId(countryId).Units);
            _units[countryId] = units;
        }
        return units;
    }
}

/// <summary>The piles a card can be in, as far as a <see cref="BoardProjection"/> tracks them.</summary>
public enum CardPile { None, Deck, Hand, Discard, Table }
