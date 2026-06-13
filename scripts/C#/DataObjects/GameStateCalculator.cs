using Godot;
using System;

using System.Collections.Generic;
using System.Linq;

public class GameStateCalculator
{
    // Cache of last calculated state per faction
    private static Dictionary<Faction, GameStateCalculator> _cachedCalculators = new Dictionary<Faction, GameStateCalculator>();
    
    public Faction Faction;
    
    // Attack state
    public List<int> TargetUnitIds = new List<int>();
    public List<UnitState> TargetUnitStates => TargetUnitIds.ToUnitStates();
    public List<int> TargetEmptyCountryIds = new List<int>(); 
    public List<CountryState> TargetEmptyCountryStates => TargetEmptyCountryIds.ToCountryStates();

    // Build/Recruit state
    public List<int> BuildableCountryIds = new List<int>();
    public List<CountryState> BuildableCountryStates => BuildableCountryIds.ToCountryStates();
    public List<int> RecruitableCountryIds = new List<int>();
    public List<CountryState> RecruitableCountryStates => RecruitableCountryIds.ToCountryStates();

    // Playable cards state
    public List<int> PlayableCardIds = new List<int>();
    public List<CardState> PlayableCardStates => CardState.ForIds(PlayableCardIds);

    // Supply state
    public List<int> InSupplyUnitIds = new List<int>();
    public List<UnitState> InSupplyUnitStates => InSupplyUnitIds.ToUnitStates();

    // Straight state (team-based, not faction-specific)
    public List<int> AxisControlledStraightIds = new List<int>();
    public List<StraightState> AxisControlledStraightStates => AxisControlledStraightIds.Select(id => GameSession.Current.GameState.StraightStates.FirstOrDefault(s => s.Id == id)).ToList();
    public List<int> AlliesControlledStraightIds = new List<int>();
    public List<StraightState> AlliesControlledStraightStates => AlliesControlledStraightIds.Select(id => GameSession.Current.GameState.StraightStates.FirstOrDefault(s => s.Id == id)).ToList();

    public bool HasTargets { get { return HasTargetUnits || HasTargetEmptyCountries; } }
    public bool HasTargetUnits { get { return TargetUnitIds.Count > 0; } }
    public bool HasTargetEmptyCountries { get { return TargetEmptyCountryIds.Count > 0; } }
    
    public List<int> TargetsOfType(UnitType unitType)
    {
        List<int> targets = TargetUnitStates.Where(unitState => unitState.Type == unitType).ToList().ToUnitIds();
        List<int> countries = TargetEmptyCountryStates.Where(countryState => countryState.Type == (unitType == UnitType.ARMY ? CountryType.LAND : CountryType.SEA)).ToList().ToCountryIds();
        targets.AddRange(countries);
        return targets;
    }
    

    private static void CalculateAttackableForFaction(Faction faction, GameStateCalculator calculator)
    {
        ClearTagsForFaction(faction, Tag.Attackable);
        
        List<int> suppliedUnitIds = GameAPI.SuppliedUnitsForFaction(faction);
        foreach (int suppliedUnitId in suppliedUnitIds)
        {
            calculator += AttackOption.CalculateAttackOptions(suppliedUnitId);
        }
        
        // Add Attackable tags to all targets for this faction
        UnitState.ForIds(calculator.TargetUnitIds).AddTag(Tag.Attackable, faction);
        CountryState.ForIds(calculator.TargetEmptyCountryIds).AddTag(Tag.Attackable, faction);
    }

    private static void CalculateBuildableCountriesForFaction(Faction faction, GameStateCalculator calculator)
    {
        ClearTagsForFaction(faction, Tag.Buildable);
        
        foreach (var countryState in CountryState.AllCountryStates)
        {
            if (countryState.CanBuild(faction))
            {
                calculator.BuildableCountryIds.Add(countryState.Id);
            }
        }
        
        CountryState.ForIds(calculator.BuildableCountryIds).AddTag(Tag.Buildable, faction);
    }

    private static void CalculateRecruitableCountriesForFaction(Faction faction, GameStateCalculator calculator)
    {
        ClearTagsForFaction(faction, Tag.Recruitable);
        
        foreach (var countryState in CountryState.AllCountryStates)
        {
            if (countryState.CanRecruit(faction))
            {
                calculator.RecruitableCountryIds.Add(countryState.Id);
            }
        }
        
        CountryState.ForIds(calculator.RecruitableCountryIds).AddTag(Tag.Recruitable, faction);
    }

    private static void CalculatePlayableCardsForFaction(Faction faction, GameStateCalculator calculator)
    {
        ClearTagsForFaction(faction, Tag.Playable);
        
        // Cards are only playable if they belong to the faction
        DeckState deckState = DeckState.ForFaction(faction);
        foreach (var cardState in deckState.HandCardStates)
        {
            // Card must have CardLogic and pass CanPlayCard() check
            if (cardState.CardLogic != null && cardState.CanPlayCard)
            {
                calculator.PlayableCardIds.Add(cardState.Id);
            }
        }
        
        CardState.ForIds(calculator.PlayableCardIds).AddTag(Tag.Playable, faction);
    }

    private static void CalculateInSupplyForFaction(Faction faction, GameStateCalculator calculator)
    {
        ClearTagsForFaction(faction, Tag.InSupply);
        
        var factionState = FactionState.ForEnum(faction);
        var pathFindingService = new PathFindingService(new PathFindingNodeDefault(), faction);
        List<UnitState> activeUnits = UnitState.ForIds(factionState.ActiveUnitIds);
        
        foreach (UnitState unit in activeUnits)
        {
            bool isSupplied = CalculateSupplyForUnit(pathFindingService, unit.Id, faction);
            if (isSupplied)
            {
                calculator.InSupplyUnitIds.Add(unit.Id);
            }
        }
        
        // Add InSupply tag only to units that are in supply
        // Units without the tag are implicitly out of supply
        UnitState.ForIds(calculator.InSupplyUnitIds).AddTag(Tag.InSupply, faction);
    }

    private static bool CalculateSupplyForUnit(PathFindingService pathFinding, int unitId, Faction faction)
    {
        var unit = UnitState.ForId(unitId);
        foreach (var supplyId in GameAPI.GetSupplyCountryIds(faction))
        {
            if (pathFinding.CalculatePath(unit.CountryId, supplyId))
            {
                if (unit.IsNavy)
                    return unit.CountryState.HasHarbor(faction);
                return true;
            }
        }
        return false;
    }

    private static void CalculateStraightControlForFaction(Faction faction, GameStateCalculator calculator)
    {
        
        // Clear old straight control tags
        foreach (var straightState in GameSession.Current.GameState.StraightStates)
        {
            straightState.Tags.RemoveForAll(Tag.AxisControlled);
            straightState.Tags.RemoveForAll(Tag.AlliesControlled);
        }
        
        // Calculate control for each straight based on controlling country's team
        foreach (var straightState in GameSession.Current.GameState.StraightStates)
        {
            FactionTeam controllingTeam = straightState.ControllingCountryState.OccupyingTeam;
            
            if (controllingTeam == FactionTeam.AXIS)
            {
                calculator.AxisControlledStraightIds.Add(straightState.Id);
                straightState.Tags.AddForAll(Tag.AxisControlled);
            }
            else if (controllingTeam == FactionTeam.ALLIES)
            {
                calculator.AlliesControlledStraightIds.Add(straightState.Id);
                straightState.Tags.AddForAll(Tag.AlliesControlled);
            }
        }
    }

    public static GameStateCalculator CalculateAllForFaction(Faction faction)
    {
        GameStateCalculator calculator = new GameStateCalculator { Faction = faction };
        
        // Calculate supply first - it's needed by buildable/attackable checks
        CalculateInSupplyForFaction(faction, calculator);
        
        CalculateAttackableForFaction(faction, calculator);
        CalculateBuildableCountriesForFaction(faction, calculator);
        CalculateRecruitableCountriesForFaction(faction, calculator);
        CalculatePlayableCardsForFaction(faction, calculator);
        CalculateStraightControlForFaction(faction, calculator);
        
        // Cache the result
        _cachedCalculators[faction] = calculator;
        return calculator;
    }
    
    public static GameStateCalculator GetCachedForFaction(Faction faction)
    {
        if (_cachedCalculators.TryGetValue(faction, out var calculator))
        {
            return calculator;
        }
        // If no cache exists, calculate and cache it
        return CalculateAllForFaction(faction);
    }

    public static Dictionary<Faction, GameStateCalculator> CalculateAll()
    {
        var calculators = new Dictionary<Faction, GameStateCalculator>();
        
        foreach (Faction faction in StaticGameData.PlayableFactions)
        {
            calculators[faction] = CalculateAllForFaction(faction);
        }
        
        return calculators;
    }

    private static void ClearTagsForFaction(Faction faction, Tag tag)
    {
        foreach (var unitState in GameSession.Current.GameState.UnitStatesById.Values)
        {
            unitState.Tags.Remove(tag, faction);
        }
        foreach (var countryState in GameSession.Current.GameState.CountryStateById.Values)
        {
            countryState.Tags.Remove(tag, faction);
        }
        foreach (var cardState in GameSession.Current.GameState.CardStatesById.Values)
        {
            cardState.Tags.Remove(tag, faction);
        }
    }
    

    public static GameStateCalculator operator +(GameStateCalculator calculator, AttackOption attackOption)
    {
        UnitState unitState = UnitState.ForId(attackOption.AttackingUnit);
        CountryState countryState = unitState.CountryState;
        foreach (int attackableUnit in attackOption.AttackableUnits)
        {
            calculator.TargetUnitIds.Add(attackableUnit);
        }
        foreach (int attackableCountry in attackOption.AttackableCountries)
        {
            calculator.TargetEmptyCountryIds.Add(attackableCountry);
        }
        return calculator;
    }    
}

