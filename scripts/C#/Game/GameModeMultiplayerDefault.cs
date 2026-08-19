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

        // The registry is static and survives returning to the menu, so a second game in the same
        // process would otherwise inherit the previous game's modifiers.
        ModifierRegistry.Clear();

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

        // The scenario states the intended rule; the host may override it in the lobby. Read here
        // rather than in StartGame because this is where the scenario is parsed, and SetupInitialGameState
        // is awaited before StartGame runs (MultiplayerSession.StartSession). Host-only, like MaxRound:
        // it gates work that reaches clients as replicated ChangeEvents, so it needs no synchronising.
        GameFlow.Instance.OpeningDiscardEnabled = GameManager.PendingOpeningDiscard ?? initialStateData.OpeningDiscard;

        GameStateCalculator.CalculateAll();
        GameStateCalculator.Enabled = false;
        await ShuffleDecks();
        await DeployUnits(initialStateData);
        await PlaceCards(initialStateData);
        await ApplyStartingVictoryPoints(initialStateData);
        await SetStartingFaction(initialStateData);
        await RegisterMutators(initialStateData);
        GameStateCalculator.Enabled = true;
        await Task.Delay(100);
    }

    /// <summary>
    /// Instantiate the scenario's mutators. Two kinds share the "mutators" array and are told apart by
    /// the type they extend:
    ///
    /// - <see cref="StepMutator"/> runs automatically at a step boundary and is registered with
    ///   ModifierRegistry. Runs before any card is played, so scenario mutators sit ahead of card
    ///   mutators in the registry — which is the tie-break when two mutators share an Order. See
    ///   StepMutatorRunner for the full ordering rule.
    /// - <see cref="ActivatableMutator"/> is activated by the player and gets one Bulletin
    ///   (a BulletinCardState) per eligible faction instead.
    ///
    /// Host-only, like the rest of setup: step mutators are evaluated server-side and their effects
    /// reach clients as replicated ChangeEvents, and the activatable ones reach clients as the
    /// RegisterBulletinCardChangeEvents emitted here.
    /// </summary>
    private async Task RegisterMutators(InitialGameStateData initialStateData)
    {
        foreach (MutatorScenarioData entry in initialStateData.Mutators)
        {
            Type mutatorType = Type.GetType(entry.Name);

            List<Faction> factionFilter = new List<Faction>();
            foreach (string factionKey in entry.Factions)
            {
                if (!Enum.TryParse<Faction>(factionKey, out Faction faction))
                    throw new Exception($"Mutator '{entry.Name}' names unknown faction '{factionKey}'.");
                factionFilter.Add(faction);
            }

            if (mutatorType != null && typeof(ActivatableMutator).IsAssignableFrom(mutatorType))
            {
                await RegisterActivatableMutator(entry, factionFilter);
                continue;
            }

            if (mutatorType == null || !typeof(StepMutator).IsAssignableFrom(mutatorType))
                throw new Exception(
                    $"Mutator '{entry.Name}' in {GameManager.PendingScenarioPath} was not found or does not extend StepMutator or ActivatableMutator.");

            StepMutator mutator = (StepMutator)Activator.CreateInstance(mutatorType);

            mutator.FactionFilter = factionFilter;
            mutator.FromRound = entry.FromRound;
            mutator.ToRound = entry.ToRound;
            mutator.Order = entry.Order;

            ModifierRegistry.Register(mutator);
            DebugUtilities.PrintPeer(
                $"Registered scenario mutator {entry.Name} ({mutator.Timing} {mutator.Step}, order {mutator.Order})");
        }

        await RegisterAlwaysAvailableMutators(initialStateData);
    }

    /// <summary>
    /// Mutators every faction has in every game, registered here rather than declared in a scenario's
    /// "mutators" array — a scenario cannot omit or disable them. Reallocate Resources is the whole
    /// point of the list: it is the standing action in the fan beside the hand, not a scenario rule.
    ///
    /// A scenario may still name one explicitly (to give it a round window, or a faction subset), and
    /// that declaration wins — the loop above has already registered it, so registering it again here
    /// would give the faction two identical Bulletins.
    /// </summary>
    private async Task RegisterAlwaysAvailableMutators(InitialGameStateData initialStateData)
    {
        List<string> alwaysAvailable = new List<string> { nameof(MutatorReallocateResources) };

        foreach (string mutatorName in alwaysAvailable)
        {
            if (initialStateData.Mutators.Any(entry => entry.Name == mutatorName))
            {
                DebugUtilities.PrintPeer($"{mutatorName} declared by the scenario; not registering it again");
                continue;
            }

            // No faction filter: RegisterActivatableMutator reads an empty list as "all playable".
            await RegisterActivatableMutator(new MutatorScenarioData { Name = mutatorName }, new List<Faction>());
        }
    }

    /// <summary>
    /// Give every eligible faction its own Bulletin for an activatable mutator — an empty faction list
    /// means all playable factions. One Bulletin per faction is what lets the whole existing card
    /// activation pipeline apply unchanged: CardLogic.Faction is then always the faction that may
    /// activate it, so the mutator's triggers and effect can use Faction directly.
    ///
    /// entry.Order is ignored: it is the tie-break for automatic step-boundary ordering, and an
    /// activatable mutator has no automatic run window.
    /// </summary>
    private async Task RegisterActivatableMutator(MutatorScenarioData entry, List<Faction> factionFilter)
    {
        List<Faction> targetFactions = factionFilter.Count > 0 ? factionFilter : StaticGameData.PlayableFactions;

        // Ids continue past the deck cards built in InstantiateFactionStates and are carried on the
        // wire, so the client uses the host's id rather than recomputing one.
        int nextCardId = gameState.CardStates.Max(cardState => cardState.Id) + 1;

        foreach (Faction faction in targetFactions)
        {
            await new RegisterBulletinCardChangeEvent(faction, nextCardId, entry.Name, entry.FromRound, entry.ToRound)
                { IsTrigger = false }.Apply();
            nextCardId++;
        }
    }

    private async Task ApplyStartingVictoryPoints(InitialGameStateData initialStateData)
    {
        // Randomized mode: the host rolls a VP for every playable faction; the values reach
        // clients through the replicated SetStartingScoreChangeEvent (clients never run setup).
        if (initialStateData.RandomizeStartingVP)
        {
            int min = initialStateData.RandomStartingVPMin;
            int max = initialStateData.RandomStartingVPMax;

            foreach (Faction faction in StaticGameData.PlayableFactions)
            {
                // Through GameRandom rather than a freshly Randomize()d RandomNumberGenerator, so a
                // seeded run reproduces its starting positions.
                int vp = GameRandom.Range(min, max);
                DebugUtilities.PrintPeer($"Random starting VP for {faction}: {vp} (range {min}-{max})");
                await new SetStartingScoreChangeEvent(faction, vp) { IsTrigger = false }.Apply();
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
            await new SetStartingScoreChangeEvent(faction, factionData.StartingVictoryPoints) { IsTrigger = false }.Apply();
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

                // Setup cannot ask anybody to free a unit — there is no player interaction here at all
                // — so an over-deploying scenario has to fail loudly instead. Checked BEFORE Apply() so
                // the throw is outside the mutate-then-broadcast window and is not mis-classified as a
                // divergence; UnitPool.GetAvailableUnitForFaction would otherwise throw from inside it.
                if (!UnitPool.FactionHasAvailableUnits(faction, deployUnitChangeEvent.UnitType))
                {
                    throw new Exception(
                        $"Scenario deploys more {deployUnitChangeEvent.UnitType} units for {faction} " +
                        $"than QGData_Factions_V2.json allows " +
                        $"({UnitState.ForIds(FactionState.ForEnum(faction).AllUnits).Count(unit => unit.Type == deployUnitChangeEvent.UnitType)} " +
                        $"available in total). Setup cannot prompt for a removal.");
                }

                await deployUnitChangeEvent.Apply();
            }
        }
    }
    /// <summary>
    /// Randomise every faction's draw deck before the opening hands are dealt.
    ///
    /// InstantiateFactionStates builds each deck in QGData_Decks.json declaration order and nothing
    /// used to disturb it, so every game dealt the same opening seven — card ids 0-6 for the first
    /// faction, and so on.
    ///
    /// Host-only by construction (SetupInitialGameState runs behind the IsServer guard in
    /// MultiplayerSession.StartSession). Clients build their decks in declaration order in Init() and
    /// receive the shuffled order through the replicated ReorderDeckChangeEvent. That indirection is
    /// required, not stylistic: ComputeHash covers deck *count* but not deck *order*, so a peer that
    /// shuffled for itself would diverge with nothing to catch it until a mismatched hand surfaced
    /// several draws later. Same pattern as RecycleCardChangeEvent's ShuffledOrder.
    ///
    /// Runs before PlaceCards, which is safe because DrawCardByName scans DeckCardIds itself and so
    /// finds a scenario's initialHandCards wherever the shuffle left them. It scans DeckCardIds
    /// rather than the DeckCardStates projection for exactly that reason — see the note on
    /// DeckState.DrawCardByName.
    /// </summary>
    private async Task ShuffleDecks()
    {
        foreach (Faction faction in StaticGameData.PlayableFactions)
        {
            DeckState deckState = DeckState.ForFaction(faction);
            if (deckState.DeckCardIds.Count == 0) continue;

            // A copy rather than deckState.ShuffleDeck(): the ChangeEvent is the mutation, here as on
            // the client, so the host does not reorder its live list ahead of the replicated event.
            List<int> shuffledOrder = new List<int>(deckState.DeckCardIds);
            GameRandom.Shuffle(shuffledOrder);

            await new ReorderDeckChangeEvent(faction, shuffledOrder) { IsTrigger = false }.Apply();
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
                // PlayAnimations = false: a scenario placing a card on the table is not a player
                // playing it, so it must not raise PlayCardChangeEvent's "X plays Y" modal — otherwise
                // setup opens one per pre-placed card. Same reasoning as the initial deploys above.
                await new PlayCardChangeEvent(cs.Id) {  IsTrigger = false, BlockAnimationQueue = false, PlayAnimations = false }.Apply();
            }

            // Move specified cards to the top of each faction's hand
            Faction faction = System.Enum.Parse<Faction>(factionKey);
            foreach (InitialHandCardEntry handCard in factionData.InitialHandCards)
            {
                var drawEvent = new DrawCardByNameChangeEvent(Faction.NONE, faction, handCard.Name) { IsTrigger = false };
                await drawEvent.Apply();
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
