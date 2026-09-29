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
        return BoardState.Live.SupplyCountryIds(faction);
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
    * Presentation of board changes. The changes themselves are BoardState operations, made by each
    * ChangeEvent's Mutate; these are the live-only halves that follow them: signals, animations and
    * notifications. Called with nothing awaited in between, so observers see the same order as before.
    */
    public static void PresentDeploy(int unitId, int countryId, bool awaitAnimation = true)
    {
        EventBus.Emit(EventBus.SignalName.UnitDeployed, unitId, countryId);
        _ = PresentationServices.Animation.Enqueue(new DeployUnitAnimation(unitId, countryId){ BlockQueue = awaitAnimation });
    }

    public static void PresentRemove(int unitId, int countryId, bool awaitAnimation = true)
    {
        _ = PresentationServices.Animation.Enqueue(new RemoveUnitAnimation(unitId, countryId){ BlockQueue = awaitAnimation });
        EventBus.Emit(EventBus.SignalName.UnitRemoved, unitId, countryId);
    }

    /**
    * Faction/ Deck API
    */
    public static async Task PresentDraw(Faction faction, int numberOfCards, List<int> drawnCardIds, bool showDrawnCards = true)
    {
        if(showDrawnCards)
        {
            if(PresentationServices.Notification.LocalPlayerControls(faction))
            {
                // Optionally show the cards that were drawn
                List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(drawnCardIds, false);
                // Label(), not WithPlayer(): this branch is gated on LocalPlayerControls, so the modal is
                // only ever shown to the faction's own controller — naming them back at themselves is noise.
                await PresentationServices.Notification.ShowModal(presentationItems, $"{faction.Label()} drew cards");
            }
            else
            {
                string message = $"Drawing {numberOfCards} card(s)...";
                PresentationServices.Notification.ShowActionText(message, faction);
            }

        }
        EventBus.Emit(EventBus.SignalName.CardsDrawn, (int)faction, numberOfCards);
    }

    public static void PresentDiscardHand(Faction faction, List<int> cardIds)
    {
        if(!PresentationServices.Notification.LocalPlayerControls(faction))
        {
            string message = $"{faction.WithPlayer()} discarded {cardIds.Count} card(s)...";
            PresentationServices.Notification.ShowActionText(message, faction);
        }
        DebugUtilities.PrintPeer($"Faction {faction} discards {cardIds.Count} card(s) from hand");
        EventBus.Emit(EventBus.SignalName.CardsDiscarded, (int)faction, cardIds.Count);
    }

    /// <summary>
    /// After the score moved on the board: the per-turn VP breakdown the scoreboard shows, and the
    /// signal. The breakdown is history for display, not board data, so it stays on GameFlow.
    /// </summary>
    public static void PresentScore(VPTurnSummary vpTurnSummary)
    {
        FactionState factionState = FactionState.ForEnum(vpTurnSummary.Faction);
        if(!GameFlow.Instance.VictoryPointSummaries.ContainsKey(vpTurnSummary.Faction))
        {
            GameFlow.Instance.VictoryPointSummaries.Add(vpTurnSummary.Faction, new List<VPTurnSummary>());
        }
        GameFlow.Instance.VictoryPointSummaries[vpTurnSummary.Faction].Add(vpTurnSummary);  
        DebugUtilities.PrintPeer($"Faction {vpTurnSummary.Faction} scored {vpTurnSummary.TotalScore} points (Total Score: {factionState.Score})");      
        EventBus.Emit(EventBus.SignalName.FactionScoredPoints, (int)vpTurnSummary.Faction, factionState.Score);
    }


    /// <summary>
    /// A card step asked for something the rules do not allow. Derives from
    /// <see cref="GameRuleException"/> so ErrorReporter can classify it as a rules/authoring bug
    /// (thrown before any state mutation, therefore always recoverable) without matching on messages.
    /// </summary>
    public class GameAPIException : GameRuleException
    {
        public GameAPIException(string message) : base(message) { }
    }
}
