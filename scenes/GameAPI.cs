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
        DebugUtilities.PrintPeer($"MultiplayerSession entered tree (IsServer={Multiplayer.IsServer()})");
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
            if (countryState.IsSupply && countryState.OccupyingFactions.Contains(faction))
            {
                response.Insert(0, countryState.Id); // push_front equivalent
            }
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
    
    public static void RequestResponseCardActivation(ChangeEvent changeEvent, Callable callback)
    {
        foreach (Faction faction in Enum.GetValues<Faction>()) // Assuming GetKeys() exists
        {
            RequestResponseCardActivationForFaction(changeEvent, (Faction)faction, callback);
        }
    }
    public static void RequestResponseCardActivationForFaction(ChangeEvent changeEvent, Faction faction, Callable callback)
    {
        if (DeckState.ForFaction(faction).ResponseCardIds.Count > 0)
        {
            // TODO: Implement callback invocation
        }
    }
    public static void RequestStatusCardActivation(ChangeEvent changeEvent, Callable callback)
    {
        foreach (Faction faction in Enum.GetValues<Faction>())
        {
            RequestResponseCardActivationForFaction(changeEvent, faction, callback);
        }
    }

    /**
    * Board Management API
    */
    public static void DeployUnitToCountry(int countryId, Faction faction, UnitType unitType, DeployType deployType)
    {   
        CountryState country = GameState.CountryStateById[countryId];
        country.DeployUnit(faction, unitType, deployType);
    }
    public static void RemoveUnitFromCountry(int unitId)
    {        
        CountryState.ForId(UnitState.ForId(unitId).CountryId).RemoveUnit(unitId);
    }

    /**
    * Faction/ Deck API
    */
    public static async Task DrawCards(Faction faction, int numberOfCards, bool showDrawnCards = true)
    {
        List<int> drawnCardIds = DeckState.ForFaction(faction).DrawCards(numberOfCards);
        if(showDrawnCards)
        {
            if(PlayerScene.Current.ControlledFactions.Contains(faction))
            {
                // Optionally show the cards that were drawn
                List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(drawnCardIds, false);
                await PresentationModal.Current.ShowModal(presentationItems, $"{faction} drew cards");                
            }
            else
            {
                string message = $"Drawing {numberOfCards} card(s)...";
                PlayerActionLabel.ShowText(message, faction);
            }
            
        }
        EventBus.Emit(EventBus.SignalName.CardsDrawn, (int)faction, numberOfCards);
    }

    public static async Task DiscardHandCards(Faction faction, List<int> cardIds)
    {
        DeckState.ForFaction(faction).DiscardHandCards(cardIds);
        if(PlayerScene.Current.ControlledFactions.Contains(faction))
        {
            
        }
        else
        {
            string message = $"{faction} discarded {cardIds.Count} card(s)...";
            PlayerActionLabel.ShowText(message, faction);
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
}
