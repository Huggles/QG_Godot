using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

public partial class GameModeMultiplayerDefault : IGameMode
{
    const string COUNTRY_DATA_PATH = "res://assets/data/QGData_Countries_V2.json";
    const string FACTIONS_DATA_PATH = "res://assets/data/QGData_Factions_V2.json";
    const string CARDS_DATA_PATH = "res://assets/data/QGData_Cards_V2.json";
    const string DECKS_DATA_PATH = "res://assets/data/QGData_Decks.json";
    const string INITIAL_GAME_STATE_BASIC_DATA_PATH = "res://assets/data/Scenario_Basic.json";
    const string INITIAL_GAME_STATE_DATA_PATH = "res://assets/data/Scenario_Debug.json";
    const string WORLD_SCENE_FILE = "res://scenes/World/WorldScene.tscn";
    
    private MultiplayerGameState gameState => MultiplayerSession.Instance.GameState;

    public GameModeMultiplayerDefault(){}

    public async Task Init(){
        DebugUtilities.PrintPeerFinest("Init Game Mode => Multiplayer Default");
        LoadDataFiles();
        InstantiateCountryStates();
        InstantiateUnitStates();
        InstantiateFactionStates();

        // Visual-only: no-op on a headless/dedicated server (authoritative state is already built above).
        PresentationServices.World.SpawnCountries();
        PresentationServices.World.SpawnUnits();

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
        foreach (Faction faction in Faction.GetValues(typeof(Faction)))
        {
            FactionData factionData = StaticGameData.FactionDataList.FirstOrDefault(fd => fd.Faction == faction);
            if (factionData != null)
            {
                FactionState factionState = new FactionState(factionData);
                gameState.FactionStates.Add(factionState);
            }
            else
            {
                FactionState factionState = new FactionState(faction);
                gameState.FactionStates.Add(factionState);
            }
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

                    CardData cardData  = StaticGameData.CardDataByName[$"{deckCardData.CardName}"];
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

    public async Task InitStartingState()
    {        
        await SetupInitialGameState();        
    }

    public async Task SetupInitialGameState()
    {
        // Load initial game state configuration from JSON
        DebugUtilities.PrintPeer($"SetupInitialGameState");
        
        using var initialStateDataFile = FileAccess.Open(GameManager.PendingScenarioPath, FileAccess.ModeFlags.Read);
        string initialStateDataString = initialStateDataFile.GetAsText();
        InitialGameStateData initialStateData = JsonSerializer.Deserialize<InitialGameStateData>(initialStateDataString);


        GameFlow.Instance.MaxRound = initialStateData.MaxRounds;

        GameStateCalculator.CalculateAll();
        GameStateCalculator.Enabled = false;
        await DeployUnits(initialStateData);
        await PlaceCards(initialStateData);
        await ApplyStartingVictoryPoints(initialStateData);
        await SetStartingFaction(initialStateData);
        GameStateCalculator.Enabled = true;
        await Task.Delay(100);
    }

    private async Task ApplyStartingVictoryPoints(InitialGameStateData initialStateData)
    {
        // Randomized mode: the host rolls a VP for every playable faction; the values reach
        // clients through the replicated SetStartingScoreChangeEvent (clients never run setup).
        if (initialStateData.RandomizeStartingVP)
        {
            int min = initialStateData.RandomStartingVPMin;
            int max = initialStateData.RandomStartingVPMax;
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            foreach (Faction faction in StaticGameData.PlayableFactions)
            {
                int vp = rng.RandiRange(min, max);
                DebugUtilities.PrintPeer($"Random starting VP for {faction}: {vp} (range {min}-{max})");
                await new SetStartingScoreChangeEvent(faction, vp) { IsTrigger = false }.ApplyChange();
            }
            return;
        }

        foreach (var (factionKey, factionData) in initialStateData.Factions)
        {
            if (factionData.StartingVictoryPoints == 0) continue;

            if (!Enum.TryParse<Faction>(factionKey, out Faction faction))
            {
                DebugUtilities.PrintPeer($"Warning: Invalid faction '{factionKey}' in initial game state config");
                continue;
            }

            DebugUtilities.PrintPeer($"Setting starting VP for {faction} to {factionData.StartingVictoryPoints}");
            await new SetStartingScoreChangeEvent(faction, factionData.StartingVictoryPoints) { IsTrigger = false }.ApplyChange();
        }
    }
    private async Task DeployUnits(InitialGameStateData initialStateData)
    {
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
                DebugUtilities.PrintPeerFinest($"Deploying unit for {factionKey} in {deployment.CountryName}");

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
                ) { IsTrigger = false, BlockAnimationQueue = false, PlayAnimations = false }; // prevent animations during initial setup
                await deployUnitChangeEvent.ApplyChange();
            }
        }
    }
    private async Task PlaceCards(InitialGameStateData initialStateData)
    {
        DebugUtilities.PrintPeer($"Placing initial cards");

        foreach (var (factionKey, factionData) in initialStateData.Factions)
        {
            // Add initial cards to play area WITHOUT activating them
            foreach (InitialCardEntry card in factionData.InitialCards)
            {            
                CardState cs = CardState.ForName(card.Name);
                if(cs == null) throw new Exception($"Card '{card.Name}' not found in initial game state config. Ensure name matches the Name field in QGData_Cards_V2.json.");
                await new PlayCardChangeEvent(cs.Id) {  IsTrigger = false }.ApplyChange();
            }

            // Move specified cards to the top of each faction's hand
            Faction faction = System.Enum.Parse<Faction>(factionKey);
            foreach (InitialHandCardEntry handCard in factionData.InitialHandCards)
            {
                var drawEvent = new DrawCardByNameChangeEvent(Faction.NONE, faction, handCard.Name) { IsTrigger = false };
                await drawEvent.ApplyChange();
            }
        }
    }

    private async Task SetStartingFaction(InitialGameStateData initialStateData)
    {
        await Task.CompletedTask;

        int count = StaticGameData.PlayableFactions.Count;

        // Default to the first faction of the round when no startingFaction is specified.
        int factionIndex = 0;
        if (!string.IsNullOrEmpty(initialStateData.StartingFaction))
        {
            if (!Enum.TryParse<Faction>(initialStateData.StartingFaction, out Faction startingFaction))
            {
                throw new Exception($"Invalid startingFaction '{initialStateData.StartingFaction}' in scenario config.");
            }

            factionIndex = StaticGameData.PlayableFactions.IndexOf(startingFaction);
            if (factionIndex < 0)
            {
                throw new Exception($"startingFaction '{initialStateData.StartingFaction}' is not in PlayableFactions.");
            }
        }

        // GameTurn is the seed the first StartNewTurn increments by 1 before the opening turn runs,
        // so CurrentFaction lands on factionIndex and Round lands on StartingRound.
        GameFlow.Instance.GameTurn = (initialStateData.StartingRound - 1) * count + factionIndex;
        DebugUtilities.PrintPeer($"Starting round {initialStateData.StartingRound}, faction index {factionIndex} (GameTurn seed: {GameFlow.Instance.GameTurn})");
    }
    
    

}
