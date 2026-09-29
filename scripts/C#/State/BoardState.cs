using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// The mutable data of one game: who stands where, the card piles, score, card and flow runtime, and
/// every tag. Conditions check it and ChangeEvents change it; neither owns it.
///
/// <see cref="Live"/> is the board the game is played on. Its records belong to the state objects
/// (CountryState, UnitState, ...), which forward to them, so existing code reads live data unchanged.
/// <see cref="Fork"/> deep-copies every record into a board of its own — "the game as it would look" —
/// that nothing live can see. Static data (topology, names, types) is not here: it never changes.
/// </summary>
public sealed partial class BoardState
{
    /// <summary>The board the game is being played on.</summary>
    public static BoardState Live => GameSession.Current.GameState.Board;

    // Non-null only for the live board: records are resolved through the state objects that own them.
    private readonly MultiplayerGameState _source;

    private readonly Dictionary<int, CountryRecord> _countries;
    private readonly Dictionary<int, UnitRecord> _units;
    // Concurrent: cards and steps can be created after a fork (a Bulletin, steps a card appends to
    // itself), and the fork then copies them in on first use, from the calculator's parallel passes.
    private readonly ConcurrentDictionary<int, CardRecord> _cards;
    private readonly Dictionary<int, StraitRecord> _straits;
    private readonly ConcurrentDictionary<int, StepRecord> _steps;
    private readonly Dictionary<Faction, FactionRecord> _factions;
    private readonly FlowRecord _flow;
    private readonly List<IModifier> _modifiers;

    public bool IsLive => _source != null;

    public BoardState(MultiplayerGameState source) => _source = source;

    private BoardState(Dictionary<int, CountryRecord> countries, Dictionary<int, UnitRecord> units,
                       ConcurrentDictionary<int, CardRecord> cards, Dictionary<int, StraitRecord> straits,
                       ConcurrentDictionary<int, StepRecord> steps, Dictionary<Faction, FactionRecord> factions, FlowRecord flow,
                       List<IModifier> modifiers)
    {
        _countries = countries; _units = units; _cards = cards; _straits = straits;
        _steps = steps; _factions = factions; _flow = flow; _modifiers = modifiers;
    }

    // ── Records ─────────────────────────────────────────────────────────────

    public CountryRecord ForCountry(int id) => _source != null ? _source.CountryStateById[id].Record : _countries[id];
    public UnitRecord ForUnit(int id) => _source != null ? _source.UnitStatesById[id].Record : _units[id];
    public CardRecord ForCard(int id) => _source != null ? _source.CardStatesById[id].Record : Of(CardState.ForId(id));
    public StepRecord ForStep(int id) => _source != null ? _source.CardStepsById[id].Record : Of(GameSession.Current.GameState.CardStepsById[id]);
    public FactionRecord ForFaction(Faction faction) =>
        _source != null ? _source.FactionStatesByFaction[faction].Record : _factions[faction];

    public IEnumerable<CountryRecord> CountryRecords =>
        _source != null ? _source.CountryStateById.Values.Select(s => s.Record) : _countries.Values;
    public IEnumerable<UnitRecord> UnitRecords =>
        _source != null ? _source.UnitStatesById.Values.Select(s => s.Record) : _units.Values;
    public IEnumerable<CardRecord> CardRecords =>
        _source != null ? _source.CardStatesById.Values.Select(s => s.Record) : CardState.All.Values.Select(Of);

    // ── Flow ────────────────────────────────────────────────────────────────
    // The live values stay on GameFlow, whose [Export]s the multiplayer synchronizer addresses by name.

    public int GameTurn
    {
        get => _source != null ? GameFlow.Instance.GameTurn : _flow.GameTurn;
        set { if (_source != null) GameFlow.Instance.GameTurn = value; else _flow.GameTurn = value; }
    }

    public TurnStep TurnStep
    {
        get => _source != null ? GameFlow.Instance.TurnStep : _flow.TurnStep;
        set { if (_source != null) GameFlow.Instance.TurnStep = value; else _flow.TurnStep = value; }
    }
    public Faction CurrentFaction => _source != null ? GameFlow.Instance.CurrentFaction : StaticGameData.FactionForTurn(_flow.GameTurn);
    public Dictionary<Faction, int> CardsPlayedThisTurnStep =>
        _source != null ? GameFlow.Instance.CardsPlayedThisTurnStep : _flow.CardsPlayedThisTurnStep;

    // ── Modifiers ───────────────────────────────────────────────────────────
    // Registration order is kept: discard modifiers clamp at zero between steps, so order can matter.

    public IEnumerable<T> Modifiers<T>() where T : IModifier =>
        _source != null ? ModifierRegistry.GetAll<T>() : _modifiers.OfType<T>();

    public void RegisterModifier(IModifier modifier)
    {
        if (_source != null) { ModifierRegistry.Register(modifier); return; }
        if (!_modifiers.Contains(modifier)) _modifiers.Add(modifier);
    }

    public void UnregisterModifier(IModifier modifier)
    {
        if (_source != null) ModifierRegistry.Unregister(modifier);
        else _modifiers.Remove(modifier);
    }

    // ── Fork ────────────────────────────────────────────────────────────────

    /// <summary>An independent deep copy. Writes to it are invisible to this board and vice versa.</summary>
    public BoardState Fork()
    {
        if (_source == null)
            return new BoardState(
                _countries.ToDictionary(kv => kv.Key, kv => kv.Value.Clone()),
                _units.ToDictionary(kv => kv.Key, kv => kv.Value.Clone()),
                new ConcurrentDictionary<int, CardRecord>(_cards.ToDictionary(kv => kv.Key, kv => kv.Value.Clone())),
                _straits.ToDictionary(kv => kv.Key, kv => kv.Value.Clone()),
                new ConcurrentDictionary<int, StepRecord>(_steps.ToDictionary(kv => kv.Key, kv => kv.Value.Clone())),
                _factions.ToDictionary(kv => kv.Key, kv => kv.Value.Clone()),
                _flow.Clone(),
                new List<IModifier>(_modifiers));

        GameFlow flow = GameFlow.Instance;
        return new BoardState(
            _source.CountryStates.ToDictionary(s => s.Id, s => s.Record.Clone()),
            _source.UnitStates.ToDictionary(s => s.Id, s => s.Record.Clone()),
            new ConcurrentDictionary<int, CardRecord>(_source.CardStates.ToDictionary(s => s.Id, s => s.Record.Clone())),
            // Keyed by controlling country: a strait's Id is never assigned, so every one is 0.
            _source.StraightStates.ToDictionary(s => s.ControllingCountryId, s => s.Record.Clone()),
            new ConcurrentDictionary<int, StepRecord>(_source.CardSteps.ToDictionary(s => s.Id, s => s.Record.Clone())),
            _source.FactionStates.ToDictionary(s => s.Faction, s => s.Record.Clone()),
            new FlowRecord
            {
                GameTurn = flow?.GameTurn ?? 0,
                TurnStep = flow?.TurnStep ?? 0,
                CardsPlayedThisTurnStep = new Dictionary<Faction, int>(flow?.CardsPlayedThisTurnStep ?? new()),
            },
            ModifierRegistry.Snapshot());
    }

    // ── Hash ────────────────────────────────────────────────────────────────

    /// <summary>
    /// The sync hash peers compare. Same fields, same order as it always had, so a fork can be hashed
    /// against the live board it is meant to predict.
    /// </summary>
    public string Hash()
    {
        MultiplayerGameState live = _source ?? GameSession.Current.GameState;
        var sb = new StringBuilder();

        foreach (CountryState cs in live.CountryStates.OrderBy(c => c.Id))
            sb.Append($"C{cs.Id}:{string.Join(",", ForCountry(cs.Id).Units.OrderBy(kv => (int)kv.Key).Select(kv => $"{(int)kv.Key}={kv.Value}"))}|");

        foreach (UnitState us in live.UnitStates.OrderBy(u => u.Id))
        {
            UnitRecord unit = ForUnit(us.Id);
            sb.Append($"U{us.Id}:{unit.CountryId},{unit.ImmuneForTurn},{unit.SuppliedForTurn}|");
        }

        // Which country controls a strait is map topology, so the live object answers for every board.
        foreach (StraightState ss in live.StraightStates.OrderBy(s => s.Id))
            sb.Append($"S{ss.Id}:{ss.ControllingCountryId}|");

        foreach (Faction faction in live.PlayableFactionStatesByFaction.Keys.OrderBy(f => (int)f))
        {
            FactionRecord fr = ForFaction(faction);
            sb.Append($"F{(int)faction}:{fr.Score}," +
                      $"d{fr.Deck.Count}," +
                      $"h{string.Join("-", fr.Hand.OrderBy(id => id))}," +
                      $"st{string.Join("-", fr.Status.OrderBy(id => id))}," +
                      $"r{string.Join("-", fr.Response.OrderBy(id => id))}|");
        }
        return Fnv1a32(sb.ToString()).ToString("X8");
    }

    // Raised and cleared by local input and preview code, never by a ChangeEvent or the calculator.
    private static readonly HashSet<Tag> LocalTags = new() { Tag.Clickable, Tag.RebuildTarget, Tag.PreviewTarget, Tag.FocusTarget };

    /// <summary>
    /// Everything this board holds, one line per object, for checking that a fork changed exactly as
    /// the live board did. Unlike <see cref="Hash"/> it covers card runtime, step tags, deck order,
    /// flow and modifiers — and every derived tag, so it also checks the fork's tag pass.
    /// </summary>
    public List<string> Digest()
    {
        MultiplayerGameState live = _source ?? GameSession.Current.GameState;
        string Tags(BoardRecord record) => record.Tags.Digest(tag => !LocalTags.Contains(tag));
        string Ids(IEnumerable<int> ids) => string.Join("-", ids);
        List<string> lines = new();

        foreach (CountryState cs in live.CountryStates.OrderBy(c => c.Id))
            lines.Add($"C{cs.Id} {string.Join(",", Of(cs).Units.OrderBy(kv => (int)kv.Key).Select(kv => $"{(int)kv.Key}={kv.Value}"))} [{Tags(Of(cs))}]");
        foreach (UnitState us in live.UnitStates.OrderBy(u => u.Id))
            lines.Add($"U{us.Id} {Of(us).CountryId} [{Tags(Of(us))}]");
        foreach (CardState card in live.CardStates.OrderBy(c => c.Id))
        {
            CardRecord r = Of(card);
            lines.Add($"K{card.Id} p{Ids(r.PlayedInTurn)} a{Ids(r.ActivatedInTurns)} {r.IsRevealed} {r.IsBlocked} [{Tags(r)}]");
        }
        foreach (CardStep step in live.CardSteps)
            lines.Add($"S{step.Id} [{Tags(Of(step))}]");
        foreach (StraightState ss in live.StraightStates.OrderBy(s => s.ControllingCountryId))
            lines.Add($"T{ss.ControllingCountryId} [{Tags(Of(ss))}]");
        foreach (FactionState fs in live.FactionStates.OrderBy(f => (int)f.Faction))
        {
            FactionRecord r = ForFaction(fs.Faction);
            lines.Add($"F{(int)fs.Faction} {r.Score} d{Ids(r.Deck)} h{Ids(r.Hand)} x{Ids(r.Discarded)} r{Ids(r.Response)} s{Ids(r.Status)} [{Tags(r)}]");
        }
        lines.Add($"flow {GameTurn} {TurnStep} {string.Join(",", CardsPlayedThisTurnStep.OrderBy(kv => (int)kv.Key).Select(kv => $"{kv.Key}={kv.Value}"))}");
        lines.Add($"mods {string.Join(",", Modifiers<IModifier>().Select(m => m is CardLogic c ? $"card{c.CardState.Id}" : m.GetType().Name))}");
        return lines;
    }

    private static uint Fnv1a32(string s)
    {
        uint hash = 2166136261u;
        foreach (char c in s) { hash ^= c; hash *= 16777619u; }
        return hash;
    }

    private sealed class FlowRecord
    {
        public int GameTurn;
        public TurnStep TurnStep;
        public Dictionary<Faction, int> CardsPlayedThisTurnStep = new();

        public FlowRecord Clone() => new()
        {
            GameTurn = GameTurn,
            TurnStep = TurnStep,
            CardsPlayedThisTurnStep = new Dictionary<Faction, int>(CardsPlayedThisTurnStep),
        };
    }
}

/// <summary>The mutable part of one board object: its tags, plus whatever its subclass adds.</summary>
public abstract class BoardRecord
{
    public TagContainer Tags { get; private set; } = new();

    protected T CloneTagsInto<T>(T copy) where T : BoardRecord
    {
        copy.Tags = Tags.CloneBits();
        return copy;
    }
}

public sealed class CountryRecord : BoardRecord
{
    public Dictionary<Faction, int> Units = new();

    public CountryRecord Clone() => CloneTagsInto(new CountryRecord { Units = new Dictionary<Faction, int>(Units) });
}

public sealed class UnitRecord : BoardRecord
{
    /// <summary>The country the unit stands in, or -1 while it is in the pool.</summary>
    public int CountryId = -1;

    public bool ImmuneForTurn
    {
        get => Tags.Has(Tag.Immune, Faction.ALL);
        set { if (value) Tags.Add(Tag.Immune, Faction.ALL); else Tags.Remove(Tag.Immune, Faction.ALL); }
    }

    public bool SuppliedForTurn
    {
        get => Tags.Has(Tag.SuppliedForTurn, Faction.ALL);
        set { if (value) Tags.Add(Tag.SuppliedForTurn, Faction.ALL); else Tags.Remove(Tag.SuppliedForTurn, Faction.ALL); }
    }

    public UnitRecord Clone() => CloneTagsInto(new UnitRecord { CountryId = CountryId });
}

public sealed class CardRecord : BoardRecord
{
    public List<int> PlayedInTurn = new();
    public List<int> ActivatedInTurns = new();
    public bool IsRevealed;
    public bool IsBlocked;

    public CardRecord Clone() => CloneTagsInto(new CardRecord
    {
        PlayedInTurn = new List<int>(PlayedInTurn),
        ActivatedInTurns = new List<int>(ActivatedInTurns),
        IsRevealed = IsRevealed,
        IsBlocked = IsBlocked,
    });
}

/// <summary>One faction's score and its five card piles.</summary>
public sealed class FactionRecord : BoardRecord
{
    public int Score;
    public List<int> Deck = new();
    public List<int> Hand = new();
    public List<int> Discarded = new();
    public List<int> Response = new();
    public List<int> Status = new();

    public FactionRecord Clone() => CloneTagsInto(new FactionRecord
    {
        Score = Score,
        Deck = new List<int>(Deck),
        Hand = new List<int>(Hand),
        Discarded = new List<int>(Discarded),
        Response = new List<int>(Response),
        Status = new List<int>(Status),
    });
}

public sealed class StraitRecord : BoardRecord
{
    public StraitRecord Clone() => CloneTagsInto(new StraitRecord());
}

public sealed class StepRecord : BoardRecord
{
    public StepRecord Clone() => CloneTagsInto(new StepRecord());
}
