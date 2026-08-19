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
    public static List<FactionData> FactionDataList { get; set; } = new();
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

    
    public static FactionTeam FactionTeamForFaction(Faction faction)
    {
        return faction switch
        {
            Faction.GERMANY or Faction.JAPAN or Faction.ITALY => FactionTeam.AXIS,
            Faction.UNITED_KINGDOM or Faction.SOVIET or Faction.UNITED_STATES => FactionTeam.ALLIES,
            _ => FactionTeam.NONE
        };
    }
    public static FactionTeam OpponentFactionTeamForFaction(Faction faction)
    {
        return faction switch
        {
            Faction.UNITED_KINGDOM or Faction.SOVIET or Faction.UNITED_STATES => FactionTeam.AXIS,
            Faction.GERMANY or Faction.JAPAN or Faction.ITALY => FactionTeam.ALLIES,
            _ => FactionTeam.NONE
        };
    }
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
        return FactionsForTeam(team).Sum(faction => FactionState.ForEnum(faction)?.Score ?? 0);
    }

   
}
