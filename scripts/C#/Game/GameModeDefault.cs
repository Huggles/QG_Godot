using Godot;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

public partial class GameModeDefault : IGameMode
{
    const string COUNTRY_DATA_PATH = "res://assets/data/QGData_Countries_V2.json";
    const string FACTIONS_DATA_PATH = "res://assets/data/QGData_Factions_V2.json";
    const string CARDS_DATA_PATH = "res://assets/data/QGData_Cards_V2.json";
    const string DECKS_DATA_PATH = "res://assets/data/QGData_Decks.json";
    
    private MultiplayerGameState gameState = GameSession.Current.GameState;

    public GameModeDefault(){}

    public async Task Init(){
        DebugUtilities.PrintPeerFinest("Init Game Mode");
        LoadDataFiles();
        InstantiateFactionStates();
        InstantiateCountryStates();
        InstantiateUnitStates();
        
        SpawnCountries();
        SpawnUnits();

        await SetupInitialGameState();

        DebugUtilities.PrintPeerFinest("Game Mode Finished Initializing");
        
    }

    public void LoadDataFiles() {
        using var countryDataFile = FileAccess.Open(COUNTRY_DATA_PATH, FileAccess.ModeFlags.Read);
        string countryDataString = countryDataFile.GetAsText();
        List<CountryData> countryDataArray      = JsonSerializer.Deserialize<List<CountryData>>(countryDataString);
        StaticGameData.CountryDataList = countryDataArray;        

        using var factionDataFile = FileAccess.Open(FACTIONS_DATA_PATH, FileAccess.ModeFlags.Read);
        string factionDataString = factionDataFile.GetAsText();
        List<FactionData> factionDataArray      = JsonSerializer.Deserialize<List<FactionData>>(factionDataString);
        foreach (FactionData factionData in factionDataArray) {
            factionData.LoadTextures();
        }

        StaticGameData.FactionDataList = factionDataArray;

        

        using var cardDataFile = FileAccess.Open(CARDS_DATA_PATH, FileAccess.ModeFlags.Read);
        string cardDataString = cardDataFile.GetAsText();
        List<CardData> cardDataArray            = JsonSerializer.Deserialize<List<CardData>>(cardDataString);
        StaticGameData.CardDataList = cardDataArray;

        using var deckDataFile = FileAccess.Open(DECKS_DATA_PATH, FileAccess.ModeFlags.Read);
        string deckDataString = deckDataFile.GetAsText();
        List<DeckData> deckDataArray            = JsonSerializer.Deserialize<List<DeckData>>(deckDataString);
        StaticGameData.DeckDataList = deckDataArray;

        foreach(CountryData countryData in countryDataArray){
            countryData.LoadData();
        }

        DebugUtilities.PrintPeerFinest("Data Finished Loading");
    }

    public void InstantiateFactionStates(){
         // First pass: create a FactionState for each FactionData
        foreach (FactionData factionData in StaticGameData.FactionDataList)
        {
            FactionState factionState = new FactionState(factionData);
            gameState.FactionStates.Add(factionState);            
        }

        int cardCounter = 0;
        // Second pass: build decks & card states
        foreach (DeckData deckData in StaticGameData.DeckDataList)
        {
            // lookup the corresponding FactionState
            FactionState factionState = FactionState.ForEnum(deckData.Faction); 
            // collect the CardState objects for this faction (if you need them later)
            var cardStatesForFaction = new List<CardState>();

            // deckData.cards is an array of Dictionaries, each with "card_name" and "number"
            foreach (DeckCardData deckCardData in deckData.Cards)
            {                

                // for each instance in the count
                for (int i = 0; i < deckCardData.Number; i++)
                {

                    CardData cardData  = StaticGameData.CardDataByName[deckCardData.CardName];
                     Type cardType = Type.GetType(cardData.ExecutionClass);
                    if(cardType == null) {            
                        throw new Exception($"ExecutionClass not found for {cardData.UniqueName}, {cardData.ExecutionClass}");
                    }
                    CardState cardState = new CardState(cardData);

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
    public void SpawnCountries(){
        foreach( CountryState countryState in gameState.CountryStates){
            CountryScene.SpawnCountry(countryState.Id);
        }
    }
    public void SpawnUnits(){
        foreach( UnitState unitState in gameState.UnitStates){
            UnitScene.SpawnUnit(unitState.Id);
        }
    }

    public async Task SetupInitialGameState()
    {
        // Load initial game state configuration from JSON
        DebugUtilities.PrintPeer($"SetupInitialGameState");
        
        using var initialStateDataFile = FileAccess.Open(GameManager.PendingScenarioPath, FileAccess.ModeFlags.Read);
        string initialStateDataString = initialStateDataFile.GetAsText();
        InitialGameStateData initialStateData = JsonSerializer.Deserialize<InitialGameStateData>(initialStateDataString);

        await DeployUnits(initialStateData);
        await PlaceCards(initialStateData);
        ApplyStartingFaction(initialStateData);

        await ReplayContext.Pace(100);
    }
    private async Task DeployUnits(InitialGameStateData initialStateData)
    {
        DebugUtilities.PrintPeer($"Doing Unit Deployments");
        foreach (var (factionKey, factionData) in initialStateData.Factions)
        {
            // Parse faction enum
            if (!Enum.TryParse<Faction>(factionKey, out Faction faction))
            {
                DebugUtilities.PrintPeer($"Warning: Invalid faction '{factionKey}' in initial game state config");
                continue;
            }

            foreach (UnitDeploymentData deployment in factionData.UnitDeployments)
            {
                DebugUtilities.PrintPeer($"Deploying unit for {factionKey} in {deployment.CountryName}");

                // Get country state by UniqueName
                CountryState countryState = CountryState.ForName(deployment.CountryName);
                if (countryState == null)
                {
                    throw new Exception($"Country '{deployment.CountryName}' not found in initial game state config. Ensure countryName matches the UniqueName field in QGData_Countries_V2.json.");
                }            
                DeployUnitChangeEvent deployUnitChangeEvent = new DeployUnitChangeEvent(
                    faction, 
                    countryState.Id, 
                    DeployType.RECRUIT
                );
                deployUnitChangeEvent.IsTrigger = false;
                DebugUtilities.PrintPeer($"DoChangeEvent");
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            }
        }
    }
    private void ApplyStartingFaction(InitialGameStateData initialStateData)
    {
        if (string.IsNullOrEmpty(initialStateData.StartingFaction)) return; 

        if (!Enum.TryParse<Faction>(initialStateData.StartingFaction, out Faction startingFaction))
        {
            throw new Exception($"Invalid startingFaction '{initialStateData.StartingFaction}' in scenario config.");
        }

        int factionIndex = StaticGameData.PlayableFactions.IndexOf(startingFaction);
        if (factionIndex < 0)
        {
            throw new Exception($"startingFaction '{initialStateData.StartingFaction}' is not in PlayableFactions.");
        }

        // GameTurn = factionIndex + 1 puts CurrentFaction at the desired faction on the first StartNewTurn increment
        GameFlow.Instance.GameTurn = factionIndex;
        DebugUtilities.PrintPeer($"Starting faction set to {startingFaction} (GameTurn offset: {factionIndex})");
    }
    private async Task PlaceCards(InitialGameStateData initialStateData)
    {
        DebugUtilities.PrintPeer($"Placing initial cards");

        // Move specified cards to the top of each faction's hand
        foreach (var (factionKey, factionData) in initialStateData.Factions)
        {
            Faction faction = System.Enum.Parse<Faction>(factionKey);
            DeckState deck = DeckState.ForFaction(faction);
            foreach (InitialHandCardEntry handCard in factionData.InitialHandCards)
            {
                int cardId = deck.DrawCardByName(handCard.Name);
                if (cardId == -1)
                    DebugUtilities.PrintPeerError($"InitialHandCard not found in deck: {handCard.Name} for {factionKey}");
            }
        }
    }
}
