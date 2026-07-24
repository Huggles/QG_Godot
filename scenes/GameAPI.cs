using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class GameAPI : Node
{
    private static GameAPI Instance
    {
        get
        {
            if (field == null)
            {
                throw new System.ArgumentNullException("GameAPI Not Initialized");
            }
            return field;
        }
        set;
    }
    
    public override void _EnterTree()
    {
        base._EnterTree();
        Instance = this;
        DebugUtilities.PrintPeerFinest($"MultiplayerSession entered tree (IsServer={Multiplayer.IsServer()})");
    }

    private static MultiplayerGameState GameState => GameSession.Current.GameState;


    /**
    * Get list of active unit ids for a faction. Note that this is not the same as the list of deployed unit ids, as some deployed units may be destroyed or otherwise inactive.
    */
    public static List<int> ActiveUnitsForFaction(Faction faction)
    {
        return FactionState.ForEnum(faction).ActiveUnitIds;
    }

    public static List<int> SuppliedUnitsForFaction(Faction faction)
    {
        return FactionState.ForEnum(faction).SuppliedUnitIds;
    }

    public static List<int> GetSupplyCountryIds(Faction faction)
    {
        var response = new List<int>();
        foreach (var countryState in GameState.CountryStates)
        {
            bool blocked = ModifierRegistry.GetAll<ISupplyBlockModifier>()
                .Any(m => m.BlocksSupply(countryState.Id, faction));
            if (!blocked && countryState.IsSupply && countryState.OccupyingFactions.Contains(faction))
                response.Insert(0, countryState.Id);
        }
        return response;
    }

    public static List<int> ActiveUnitIds(Faction faction)
    {
        var response = new List<int>();
        foreach (UnitState unitState in GameState.UnitStates)
        {
            if (unitState.Faction == faction && unitState.CountryId >= 0)
            {
                response.Add(unitState.Id);
            }
        }
        return response;
    }

    public static List<int> OccupiedCountryIds(Faction faction)
    {
        var response = new List<int>();
        foreach (var unitId in ActiveUnitIds(faction))
        {
            response.Add(UnitState.ForId(unitId).CountryId);
        }
        return response;
    }
    public static List<int> SuppliedUnitIds(Faction faction)
    {
        var response = new List<int>();
        foreach (var unitId in ActiveUnitIds(faction))
        {
            var unitState = UnitState.ForId(unitId);
            if (unitState.InSupply)
            {
                response.Add(unitId);
            }
        }
        return response;
    }
    public static List<int> UnsuppliedUnitIds(Faction faction)
    {
        var response = new List<int>();
        foreach (var unitId in ActiveUnitIds(faction))
        {
            var unitState = UnitState.ForId(unitId);
            if (!unitState.InSupply)
            {
                response.Add(unitId);
            }
        }
        return response;
    }
    public static StraightState StraightStateForNeighbors(int countryId1, int countryId2)
    {
        var matches = GameState.StraightStates.Where(straight =>
            (straight.ControlledCountryId1 == countryId1 && straight.ControlledCountryId2 == countryId2) ||
            (straight.ControlledCountryId1 == countryId2 && straight.ControlledCountryId2 == countryId1)).ToList();

        return matches.Count > 0 ? matches[0] : null;
    }    

    /**
    * Board Management API
    */
    public static int DeployUnitToCountry(int countryId, Faction faction, UnitType unitType, DeployType deployType, bool awaitAnimation = true)
    {   
        DebugUtilities.PrintPeer($"Deploying unit of type {unitType} for faction {faction} to country {countryId} with deploy type {deployType}");
        CountryState countryState = GameState.CountryStateById[countryId];

        int unitId = UnitPool.GetAvailableUnitForFaction(faction, unitType);
        if(unitId == -1) throw new Exception($"No available units of type {unitType} for faction {faction}");
        UnitState unitState = UnitState.ForId(unitId);

        bool deployable = deployType == DeployType.BUILD ? countryState.Tags.Has(Tag.Buildable, faction) : countryState.Tags.Has(Tag.Recruitable, faction);

        if (!countryState.IsCountryFull && deployable)
        {
            countryState.Units[faction] = unitState.Id;
            unitState.CountryId = countryState.Id;
            EventBus.Emit(EventBus.SignalName.UnitDeployed, unitState.Id, countryState.Id);
            _ = PresentationServices.Animation.Enqueue(new DeployUnitAnimation(unitId, countryId){ BlockQueue = awaitAnimation });
        } else {
            string exceptionMessage = $"Cannot {deployType} unit of type {unitType} for faction {faction} to country {countryState.StaticCountryData.Label}. Country is full or not deployable.";
            DebugUtilities.PrintPeer(exceptionMessage);
            throw new GameAPIException(exceptionMessage);
        }
        
        return unitId;
    }
    public static void RemoveUnitFromCountry(int unitId, bool awaitAnimation = true)
    {   
        UnitState unitState = UnitState.ForId(unitId);
        CountryState countryState = CountryState.ForId(unitState.CountryId);

        _ = PresentationServices.Animation.Enqueue(new RemoveUnitAnimation(unitId, countryState.Id){ BlockQueue = awaitAnimation });

        unitState.CountryId = -1;
        countryState.Units.Remove(unitState.Faction);
        EventBus.Emit(EventBus.SignalName.UnitRemoved, unitId, countryState.Id);
    }

    /**
    * Faction/ Deck API
    */
    public static async Task DrawCards(Faction faction, int numberOfCards, bool showDrawnCards = true)
    {
        List<int> drawnCardIds = DeckState.ForFaction(faction).DrawCards(numberOfCards);
        if(showDrawnCards)
        {
            if(PresentationServices.Notification.LocalPlayerControls(faction))
            {
                // Optionally show the cards that were drawn
                List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(drawnCardIds, false);
                await PresentationServices.Notification.ShowModal(presentationItems, $"{faction} drew cards");
            }
            else
            {
                string message = $"Drawing {numberOfCards} card(s)...";
                PresentationServices.Notification.ShowActionText(message, faction);
            }

        }
        EventBus.Emit(EventBus.SignalName.CardsDrawn, (int)faction, numberOfCards);
    }

    public static async Task DiscardHandCards(Faction faction, List<int> cardIds)
    {
        DeckState.ForFaction(faction).DiscardHandCards(cardIds);
        if(!PresentationServices.Notification.LocalPlayerControls(faction))
        {
            string message = $"{faction} discarded {cardIds.Count} card(s)...";
            PresentationServices.Notification.ShowActionText(message, faction);
        }
        DebugUtilities.PrintPeer($"Faction {faction} discards {cardIds.Count} card(s) from hand");
        EventBus.Emit(EventBus.SignalName.CardsDiscarded, (int)faction, cardIds.Count);
    }

    public static void ScorePoints(VPTurnSummary vpTurnSummary)
    {
        FactionState factionState = FactionState.ForEnum(vpTurnSummary.Faction);
        factionState.Score += vpTurnSummary.TotalScore;
        if(!GameFlow.Instance.VictoryPointSummaries.ContainsKey(vpTurnSummary.Faction))
        {
            GameFlow.Instance.VictoryPointSummaries.Add(vpTurnSummary.Faction, new List<VPTurnSummary>());
        }
        GameFlow.Instance.VictoryPointSummaries[vpTurnSummary.Faction].Add(vpTurnSummary);  
        DebugUtilities.PrintPeer($"Faction {vpTurnSummary.Faction} scored {vpTurnSummary.TotalScore} points (Total Score: {factionState.Score})");      
        EventBus.Emit(EventBus.SignalName.FactionScoredPoints, (int)vpTurnSummary.Faction, factionState.Score);
    }

    public class GameAPIException : Exception
    {
        public GameAPIException(string message) : base(message) { }
    }
}
