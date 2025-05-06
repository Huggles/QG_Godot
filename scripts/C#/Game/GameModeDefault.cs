using Godot;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text.Json;

public partial class GameModeDefault : IGameMode
{
    const string COUNTRY_DATA_PATH = "res://assets/data/QGData_Countries_V2.json";
    const string FACTIONS_DATA_PATH = "res://assets/data/QGData_Factions_V2.json";
    const string CARDS_DATA_PATH = "res://assets/data/QGData_Cards_V2.json";
    const string DECKS_DATA_PATH = "res://assets/data/QGData_Decks.json";	
    
    const string WORLD_SCENE_FILE = "res://scenes/World/WorldScene.tscn";
    
    private GameState gameState = GameSession.Instance.GameState;

    public GameModeDefault(){}

    public void Init(){
        DebugUtilities.PrintPeer("Init Game Mode");
        LoadDataFiles();
        InstantiateFactionStates();
        InstantiateCountryStates();
        InstantiateUnitStates();

        SpawnWorld();
        SpawnCountries();
        SpawnUnits();

        SetupInitialGameState();

        DebugUtilities.PrintPeer("Game Mode Finished Initializing");
        
    }

    public void LoadDataFiles() {
        using var countryDataFile = FileAccess.Open(COUNTRY_DATA_PATH, FileAccess.ModeFlags.Read);
        string countryDataString = countryDataFile.GetAsText();
        List<CountryData> countryDataArray      = JsonSerializer.Deserialize<List<CountryData>>(countryDataString);
        StaticGameData.CountryDataList = countryDataArray;

        using var factionDataFile = FileAccess.Open(FACTIONS_DATA_PATH, FileAccess.ModeFlags.Read);
        string factionDataString = factionDataFile.GetAsText();
        List<FactionData> factionDataArray      = JsonSerializer.Deserialize<List<FactionData>>(factionDataString);
        StaticGameData.FactionDataList = factionDataArray;

        using var cardDataFile = FileAccess.Open(CARDS_DATA_PATH, FileAccess.ModeFlags.Read);
        string cardDataString = cardDataFile.GetAsText();
        List<CardData> cardDataArray            = JsonSerializer.Deserialize<List<CardData>>(cardDataString);
        StaticGameData.CardDataList = cardDataArray;

        using var deckDataFile = FileAccess.Open(DECKS_DATA_PATH, FileAccess.ModeFlags.Read);
        string deckDataString = deckDataFile.GetAsText();
        List<DeckData> deckDataArray            = JsonSerializer.Deserialize<List<DeckData>>(deckDataString);
        StaticGameData.DeckDataList = deckDataArray;

        DebugUtilities.PrintPeer("Data Finished Loading");
    }

    public void InstantiateFactionStates(){
         // First pass: create a FactionState for each FactionData
        foreach (FactionData factionData in StaticGameData.FactionDataList)
        {
            FactionState factionState = new FactionState(factionData);
            gameState.FactionStates[factionState.Faction] = factionState;            
        }

        int cardCounter = 0;
        // Second pass: build decks & card states
        foreach (DeckData deckData in StaticGameData.DeckDataList)
        {
            // lookup the corresponding FactionState
            FactionState factionState = gameState.FactionStates[deckData.Faction];
            // collect the CardState objects for this faction (if you need them later)
            var cardStatesForFaction = new List<CardState>();

            // deckData.cards is an array of Dictionaries, each with "card_name" and "number"
            foreach (DeckCardData deckCardData in deckData.Cards)
            {                

                // for each instance in the count
                for (int i = 0; i < deckCardData.Number; i++)
                {
                    var cardData  = StaticGameData.CardDataByName[deckCardData.CardName];
                    var cardState = new CardState(cardData);

                    cardState.Id      = cardCounter;
                    cardState.Faction = factionState.Faction;

                    // add to the master list
                    gameState.CardStates.Add(cardState);

                    // increment and register in both lists
                    cardCounter++;
                    cardStatesForFaction.Add(cardState);
                    factionState.DeckState.DeckCardIds.Add(cardState.Id);
                }
            }
        }

    }
    public void InstantiateCountryStates(){
        //Generate Country States
        foreach(CountryData countryData in StaticGameData.CountryDataList){
            CountryState countryState = new CountryState(countryData);
            gameState.CountryStates.Add(countryState);
        }

        //Connect neighboring country states
        foreach(CountryState countryState in gameState.CountryStates){
            countryState.InitNeighborCountryStateArray();
        }

        //Initiate straights for countries.
        foreach(CountryData countryData in StaticGameData.CountryDataList){
            CountryState countryState = CountryState.ForName(countryData.UniqueName);
            StraightState straightState = new StraightState(countryState.Id, countryData.StraightData);
            countryState.StraightState = straightState;
            gameState.StraightStates.Add(straightState);
        }
    }
    public void InstantiateUnitStates(){
        foreach(FactionData factionData in StaticGameData.FactionDataList){
            for(int i = 0; i < factionData.NumberOfArmyUnits; i++){
                UnitState unitState = new UnitState(UnitType.ARMY, factionData.Faction);
                gameState.UnitStates.Add(unitState);
            }
            for(int i = 0; i < factionData.NumberOfNavyUnits; i++){
                UnitState unitState = new UnitState(UnitType.NAVY, factionData.Faction);
                gameState.UnitStates.Add(unitState);
            }
        }
    }

    public void SpawnWorld(){
        PackedScene worldScenePackage = GD.Load<PackedScene>(WORLD_SCENE_FILE);
        var worldSceneInstance = worldScenePackage.Instantiate();
        Node3D worldNode = NodeUtilities.Instance.WorldNode;
        worldNode.AddChild(worldSceneInstance);        
    }
    public void SpawnCountries(){
        foreach( CountryState countryState in gameState.CountryStates){
            countryState.InitNode();
        }
    }
    public void SpawnUnits(){
        foreach( UnitState unitState in gameState.UnitStates){
            unitState.InitNode();
        }
    }

    public void SetupInitialGameState(){
        foreach(FactionData factionData in StaticGameData.FactionDataList){            
            CountryState homespaceCountryState = CountryState.ForName(factionData.Homespace);
            DeployUnitChangeEvent deployUnitChangeEvent = new DeployUnitChangeEvent(factionData.Faction, homespaceCountryState.Id, DeployType.RECRUIT);
            deployUnitChangeEvent.ApplyChange();
        }
    }
}
