using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;


public static partial class StaticGameData
{

    public static List<Faction> PlayableFactions = [Faction.GERMANY, Faction.UNITED_KINGDOM, Faction.JAPAN, Faction.SOVIET, Faction.ITALY, Faction.UNITED_STATES];
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
                foreach (var unit in GameSession.Instance.GameState.UnitStates)
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

        DebugUtilities.PrintPeer("Init factions constants");

        var factionRows = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(dataMap["faction_data"]);
        var cardRows = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(dataMap["cards_data"]);
        var deckRows = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(dataMap["deck_data"]);
        var countryRows = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(dataMap["country_data"]);

        foreach (var row in factionRows)
            FactionDataList.Add(new FactionData());

        DebugUtilities.PrintPeer($"Loaded {FactionDataList.Count} factions");

        foreach (var row in cardRows)
            CardDataList.Add(new CardData());

        DebugUtilities.PrintPeer($"Loaded {CardDataList.Count} cards");

        foreach (var row in deckRows)
            DeckDataList.Add(new DeckData());

        DebugUtilities.PrintPeer($"Loaded {DeckDataList.Count} decks");

        foreach (var row in countryRows)
            CountryDataList.Add(new CountryData());

        DebugUtilities.PrintPeer($"Loaded {CountryDataList.Count} countries");
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

   
}
