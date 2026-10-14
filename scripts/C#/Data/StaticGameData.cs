using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;


public static partial class StaticGameData
{

    public static List<Faction> PlayableFactions = [Faction.GERMANY, Faction.UNITED_KINGDOM, Faction.JAPAN, Faction.SOVIET, Faction.ITALY, Faction.UNITED_STATES];

    /// <summary>Steady-state hand size: the DRAW step's target and the DISCARD step's cap.</summary>
    public const int HandSize = 7;

    /// <summary>
    /// Dealt once, in GameFlow.StartGame. Larger than <see cref="HandSize"/> on purpose: every
    /// faction immediately discards <see cref="OpeningDiscardCount"/> of them, so the opening choice
    /// is which seven to keep rather than which seven you were given.
    /// </summary>
    public const int OpeningHandSize = 10;

    /// <summary>
    /// Discarded by every faction before the first turn starts. OpeningHandSize - this must equal
    /// <see cref="HandSize"/>, or turn one starts off the steady-state hand size.
    /// </summary>
    public const int OpeningDiscardCount = 3;

    /// <summary>
    /// The 1-based round a 1-based game turn belongs to — every faction takes one turn per round.
    /// </summary>
    public static int RoundForTurn(int turnNumber) => ((turnNumber - 1) / PlayableFactions.Count) + 1;
    /// <summary>Whose turn a turn number is. Germany before the first turn.</summary>
    public static Faction FactionForTurn(int turnNumber) =>
        turnNumber > 0 ? PlayableFactions[(turnNumber - 1) % PlayableFactions.Count] : Faction.GERMANY;
    public static List<FactionData> FactionDataList { get; set; } = new();

    // ── Minor factions ──────────────────────────────────────────────────────
    //
    // A minor faction (France, China) is one whose FactionData names a PatronFaction. It has pieces on
    // the board but no seat, no deck and no turn: its patron checks its supply, scores it and answers
    // its prompts. Which ones exist is decided by the faction data the session loaded, so the base game
    // has none and every list below collapses to exactly what it was before minor factions existed.

    private static List<FactionData> _minorFactionsSource;
    private static List<Faction> _minorFactions = new();
    private static List<Faction> _boardFactions = PlayableFactions;

    /// <summary>The minor factions in this session, in faction-data order (empty for the base game).</summary>
    public static List<Faction> MinorFactions
    {
        get
        {
            RefreshFactionLists();
            return _minorFactions;
        }
    }

    /// <summary>
    /// Every faction with pieces on the board this session: <see cref="PlayableFactions"/>, then the
    /// minor factions. For anything about the board (supply, buildability, scoring, the sync hash);
    /// turn order and everything about cards and seats stay on PlayableFactions.
    /// </summary>
    public static List<Faction> BoardFactions
    {
        get
        {
            RefreshFactionLists();
            return _boardFactions;
        }
    }

    // Keyed on the list instance: LoadDataFiles replaces FactionDataList wholesale for every session.
    // Rebuilt into fresh lists rather than mutated, because the calculator iterates these in parallel.
    private static void RefreshFactionLists()
    {
        List<FactionData> source = FactionDataList;
        if (ReferenceEquals(source, _minorFactionsSource)) return;

        List<Faction> minors = source.Where(data => data.IsMinor).Select(data => data.Faction).ToList();
        _boardFactions = minors.Count == 0 ? PlayableFactions : PlayableFactions.Concat(minors).ToList();
        _minorFactions = minors;
        _minorFactionsSource = source;
    }

    /// <summary>The playable faction acting for <paramref name="faction"/>, or NONE if it is not a minor faction.</summary>
    public static Faction PatronOf(Faction faction)
        => FactionDataMap.GetValueOrDefault(faction) is { IsMinor: true } data ? data.Patron : Faction.NONE;

    /// <summary>The minor factions <paramref name="patron"/> acts for. Empty for the base game.</summary>
    public static List<Faction> MinorsOf(Faction patron)
        => MinorFactions.Count == 0 ? new List<Faction>() : MinorFactions.Where(minor => PatronOf(minor) == patron).ToList();

    /// <summary>The faction that answers for <paramref name="faction"/>: its patron if it is a minor faction, else itself.</summary>
    public static Faction ControllerOf(Faction faction)
        => PatronOf(faction) is var patron && patron != Faction.NONE ? patron : faction;

    /// <summary>Every faction of <paramref name="team"/> with pieces on the board, minor factions included.</summary>
    public static List<Faction> BoardFactionsForTeam(FactionTeam team)
        => BoardFactions.Where(faction => FactionTeamForFaction(faction) == team).ToList();

    /// <summary>
    /// How many factions can share one space. A space never holds both teams (CountryState.CanRecruit),
    /// so it is the size of the largest team on the board: 3 in the base game, 5 when France and China
    /// join the Allies.
    /// </summary>
    public static int MaxFactionsPerSpace
        => Math.Max(BoardFactionsForTeam(FactionTeam.AXIS).Count, BoardFactionsForTeam(FactionTeam.ALLIES).Count);
    public static List<CardData> CardDataList { get; set; } = new();
    public static List<DeckData> DeckDataList { get; set; } = new();
    public static List<CountryData> CountryDataList { get; set; } = new();

    private static Dictionary<Faction, FactionData> _factionDataMap;
    public static Dictionary<Faction, FactionData> FactionDataMap
    {
        get
        {
            if (_factionDataMap == null || _factionDataMap.Count != FactionDataList.Count)
            {
                _factionDataMap = new();
                foreach (var fd in FactionDataList)
                    _factionDataMap[fd.Faction] = fd;
            }
            return _factionDataMap;
        }
    }

    private static Dictionary<string, CardData> _cardDataByName;
    public static Dictionary<string, CardData> CardDataByName
    {
        get
        {
            if (_cardDataByName == null || _cardDataByName.Count == 0)
            {
                _cardDataByName = new();
                foreach (var cd in CardDataList)
                    _cardDataByName[cd.UniqueName] = cd;
            }
            return _cardDataByName;
        }
    }

    private static Dictionary<int, CardData> _cardDataByNumber;
    public static Dictionary<int, CardData> CardDataByNumber
    {
        get
        {
            if (_cardDataByNumber == null || _cardDataByNumber.Count == 0)
            {
                _cardDataByNumber = new();
                foreach (var cd in CardDataList)
                    if (int.TryParse(cd.Number, out int num))
                        _cardDataByNumber[num] = cd;
            }
            return _cardDataByNumber;
        }
    }

    private static Dictionary<Faction, DeckData> _deckDataMap;
    public static Dictionary<Faction, DeckData> DeckDataMap
    {
        get
        {
            if (_deckDataMap == null || _deckDataMap.Count == 0)
            {
                _deckDataMap = new();
                foreach (var dd in DeckDataList)
                    _deckDataMap[dd.Faction] = dd;
            }
            return _deckDataMap;
        }
    }

    private static Dictionary<string, CountryData> _countriesByName;
    public static Dictionary<string, CountryData> CountriesByName
    {
        get
        {
            if (_countriesByName == null || _countriesByName.Count == 0)
            {
                _countriesByName = new();
                foreach (CountryData countryData in CountryDataList)
                {
                    _countriesByName[countryData.UniqueName] = countryData;
                }

            }
            return _countriesByName;
        }
    }

    private static Dictionary<int, CountryData> _countriesById;
    public static Dictionary<int, CountryData> CountriesById
    {
        get
        {
            if (_countriesById == null || _countriesById.Count == 0)
            {
                _countriesById = new();
                foreach (CountryData countryData in CountryDataList)
                {
                    _countriesById[countryData.Number] = countryData;
                }
            }
            return _countriesById;
        }
    }

    private static Dictionary<Faction, List<UnitState>> _unitStatesByFaction;
    public static Dictionary<Faction, List<UnitState>> UnitStatesByFaction
    {
        get
        {
            if (_unitStatesByFaction == null || _unitStatesByFaction.Count == 0)
            {
                _unitStatesByFaction = new();
                foreach (var unit in GameSession.Current.GameState.UnitStates)
                {
                    var faction = unit.Faction;
                    if (!_unitStatesByFaction.ContainsKey(faction))
                        _unitStatesByFaction[faction] = new List<UnitState>();
                    _unitStatesByFaction[faction].Add(unit);
                }
            }
            return _unitStatesByFaction;
        }
    }

    public static void InitStaticData(string data)
    {
        var dataMap = JsonSerializer.Deserialize<Dictionary<string, string>>(data);

        DebugUtilities.PrintPeerFinest("Init factions constants");

        var factionRows = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(dataMap["faction_data"]);
        var cardRows = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(dataMap["cards_data"]);
        var deckRows = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(dataMap["deck_data"]);
        var countryRows = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(dataMap["country_data"]);

        foreach (var row in factionRows)
            FactionDataList.Add(new FactionData());

        DebugUtilities.PrintPeerFinest($"Loaded {FactionDataList.Count} factions");

        foreach (var row in cardRows)
            CardDataList.Add(new CardData());

        DebugUtilities.PrintPeerFinest($"Loaded {CardDataList.Count} cards");

        foreach (var row in deckRows)
            DeckDataList.Add(new DeckData());

        DebugUtilities.PrintPeerFinest($"Loaded {DeckDataList.Count} decks");

        foreach (var row in countryRows)
            CountryDataList.Add(new CountryData());

        DebugUtilities.PrintPeerFinest($"Loaded {CountryDataList.Count} countries");
    }

    
    /// <summary>
    /// The color a side is drawn in by the TacticalTeam map overlay. Teams carry no color of their own in
    /// the faction data — ColorString is per faction — so the two sides are named here rather than being
    /// derived from a member faction, which would have made the whole side read as one of its members.
    /// NONE and ALL are not sides anyone occupies a country as; they fall back to a neutral grey so a
    /// stray Faction.NONE unit is visible rather than silently painted as one of the two teams.
    /// </summary>
    public static Color FactionTeamColor(FactionTeam team)
    {
        return team switch
        {
            FactionTeam.AXIS => new Color(0.4f, 0.16f, 0.16f, 1),
            FactionTeam.ALLIES => new Color(0.16f, 0.38f, 0.6f, 1),
            _ => new Color(0.5f, 0.5f, 0.5f, 1)
        };
    }

    public static FactionTeam FactionTeamForFaction(Faction faction)
    {
        return faction switch
        {
            Faction.GERMANY or Faction.JAPAN or Faction.ITALY => FactionTeam.AXIS,
            Faction.UNITED_KINGDOM or Faction.SOVIET or Faction.UNITED_STATES => FactionTeam.ALLIES,
            _ => MinorFactionTeam(faction)
        };
    }
    public static FactionTeam OpponentFactionTeamForFaction(Faction faction)
    {
        return faction switch
        {
            Faction.UNITED_KINGDOM or Faction.SOVIET or Faction.UNITED_STATES => FactionTeam.AXIS,
            Faction.GERMANY or Faction.JAPAN or Faction.ITALY => FactionTeam.ALLIES,
            _ => OpponentTeam(MinorFactionTeam(faction))
        };
    }

    /// <summary>A minor faction's team, from its faction data. NONE for anything else (NONE, ALL).</summary>
    private static FactionTeam MinorFactionTeam(Faction faction)
        => FactionDataMap.GetValueOrDefault(faction) is { IsMinor: true } data
           && Enum.TryParse(data.Team, out FactionTeam team) ? team : FactionTeam.NONE;
    /// <summary>
    /// The other of the two playing teams. NONE and ALL have no opposite and map to themselves, so a
    /// caller alternating between the teams of a reaction window terminates rather than ping-ponging
    /// on a trigger that belongs to neither side.
    /// </summary>
    public static FactionTeam OpponentTeam(FactionTeam team)
    {
        return team switch
        {
            FactionTeam.AXIS => FactionTeam.ALLIES,
            FactionTeam.ALLIES => FactionTeam.AXIS,
            _ => team
        };
    }
    public static List<Faction> FactionsForTeam(FactionTeam team)
    {
        return team switch
        {
            FactionTeam.AXIS => new() { Faction.GERMANY, Faction.JAPAN, Faction.ITALY },
            FactionTeam.ALLIES => new() { Faction.UNITED_KINGDOM, Faction.SOVIET, Faction.UNITED_STATES },
            _ => new()
        };
    }
    public static List<Faction> OpponentFactionsForTeam(FactionTeam team)
    {
        return team switch
        {
            FactionTeam.ALLIES => new() { Faction.GERMANY, Faction.JAPAN, Faction.ITALY },
            FactionTeam.AXIS => new() { Faction.UNITED_KINGDOM, Faction.SOVIET, Faction.UNITED_STATES },
            _ => new()
        };
    }

    /// <summary>
    /// A team's running victory-point total — the number GameFlow's 30-point game-end check compares,
    /// and what the Axis/Allies flags either side of the faction strip display.
    ///
    /// Guarded because the UI can ask before there is a game state to ask about: FactionState.ForEnum
    /// dereferences MultiplayerSession.Instance.GameState, and returns null for a faction that is not
    /// in the current state.
    /// </summary>
    public static int ScoreForTeam(FactionTeam team)
    {
        if (MultiplayerSession.Instance?.GameState == null) return 0;
        // Board factions, not FactionsForTeam: a minor faction's points count for its team.
        return BoardFactionsForTeam(team).Sum(faction => FactionState.ForEnum(faction)?.Score ?? 0);
    }

   
}
