using Godot;
using System;

using System.Collections.Generic;
using System.Linq;

public class GameStateCalculator
{    
    public Faction Faction;
    

    private static void CalculateAttackableForFaction(Faction faction)
    {
        ClearTagsForFaction(faction, Tag.Attackable);
        
        List<int> suppliedUnitIds = GameAPI.SuppliedUnitsForFaction(faction);
        foreach (int suppliedUnitId in suppliedUnitIds)
        {
            AttackOption attackOption = AttackOption.CalculateAttackOptions(suppliedUnitId);
            UnitState.ForIds(attackOption.AttackableUnits).AddTag(Tag.Attackable, faction);
            CountryState.ForIds(attackOption.AttackableCountries).AddTag(Tag.Attackable, faction);
        }
    }

    private static void CalculateBuildableCountriesForFaction(Faction faction)
    {
        ClearTagsForFaction(faction, Tag.Buildable);
        
        foreach (var countryState in CountryState.AllCountryStates)
        {
            if (countryState.CanBuild(faction))
                countryState.AddTag(Tag.Buildable, faction);
        }
    }

    private static void CalculateRecruitableCountriesForFaction(Faction faction)
    {
        ClearTagsForFaction(faction, Tag.Recruitable);
        
        foreach (var countryState in CountryState.AllCountryStates)
        {
            if (countryState.CanRecruit(faction))
                countryState.AddTag(Tag.Recruitable, faction);
        }
    }

    private static void CalculateActivatableCardsForFaction(Faction faction)
    {
        ClearTagsForFaction(faction, Tag.IsActivatable);

        DeckState deckState = DeckState.ForFaction(faction);
        List<CardState> candidates = new();        
        candidates.AddRange(deckState.HandCardStates);
        candidates.AddRange(deckState.StatusCardStates);
        candidates.AddRange(deckState.ResponseCardStates);

        List<CardState> cardPlayedThisTurn = CardState.AllForFaction(faction).Values.Where(cs => cs.PlayedInTurn.Contains(GameFlow.Instance.GameTurn)).ToList();
        candidates.AddRange(cardPlayedThisTurn);

        candidates = candidates.Distinct().ToList();
        

        foreach (var cardState in candidates)
        {
            if (cardState.CardLogic == null) { DebugUtilities.PrintPeer($"[DIAG]   {cardState.CardData?.UniqueName ?? "?"} skipped - CardLogic is null"); continue; }

            bool canActivate = cardState.CardLogic.CanBeActivated();
            if (canActivate)
            {           
                DebugUtilities.PrintPeer($"Card {cardState.CardData.UniqueName} is activatable for {faction}");                 
                cardState.AddTag(Tag.IsActivatable, faction);
            }
        };  
    }

    private static void CalculatePlayedCardsForFaction(Faction faction)
    {
        ClearTagsForFaction(faction, Tag.IsPlayed);

        DeckState deckState = DeckState.ForFaction(faction);

        // STATUS and RESPONSE cards are played once they are in their permanent piles
        CardState.ForIds(deckState.StatusCardIds).AddTag(Tag.IsPlayed, faction);
        CardState.ForIds(deckState.ResponseCardIds).AddTag(Tag.IsPlayed, faction);

        // EVENT cards are played once discarded
        CardState.ForIds(deckState.DiscardedCardIds).AddTag(Tag.IsPlayed, faction);

        // EVENT cards currently in the active play round pool are also considered played
        // (covers the window between entering the pool and being moved to discard)
        if (CardPlayRound.Current != null)
        {
            foreach (var cardState in CardPlayRound.Current.CardPool)
            {
                if (cardState.Faction == faction)
                    cardState.AddTag(Tag.IsPlayed, faction);
            }
        }
    }

    private static void CalculateAfterReactionCardsForFaction(Faction faction)
    {
        ClearTagsForFaction(faction, Tag.IsAfterReaction);
        CardState.AllForFaction(faction).Values.ToList().ForEach(cardState =>
        {
            if (cardState.HasTag(Tag.IsActivatable, faction) && cardState.CardLogic?.IsBlockReaction == false)
                cardState.AddTag(Tag.IsAfterReaction, faction);
        });

        
    }

    private static void CalculatePlayableCardsForFaction(Faction faction)
    {
        ClearTagsForFaction(faction, Tag.IsPlayable);        
        DeckState deckState = DeckState.ForFaction(faction);
        deckState.HandCardStates.Where(cs => cs.HasTag(Tag.IsActivatable, faction) && !cs.IsPlayed).AddTag(Tag.IsPlayable, faction);
    }

    private static void CalculateInSupplyForFaction(Faction faction)
    {
        ClearTagsForFaction(faction, Tag.InSupply);
        ClearTagsForFaction(faction, Tag.OutOfSupply);
        
        var factionState = FactionState.ForEnum(faction);
        var pathFindingService = new PathFindingService(new PathFindingNodeDefault(), faction);
        List<UnitState> activeUnits = UnitState.ForIds(factionState.ActiveUnitIds);
        
        foreach (UnitState unit in activeUnits)
        {
            if (CalculateSupplyForUnit(pathFindingService, unit.Id, faction))
            {
                unit.AddTag(Tag.InSupply, faction);
            } 
            else
            {
                unit.AddTag(Tag.OutOfSupply, faction);
            }
        }
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

    private static void CalculateExecutableStepsForFaction(Faction faction)
    {        
        CardStep.All.ForEach(step => step.Tags.Remove(Tag.IsExecutable, faction));
        CardStep.All.ForEach(step =>
        {
            bool stepFinished = step.StepFinished;
            bool meetConditions = step.MeetAllConditions;
            bool isExecutable = !stepFinished && step.MeetAllConditions;
            if (isExecutable) 
                step.Tags.Add(Tag.IsExecutable, faction);
        });
    }

    private static void CalculateStraightControlForFaction(Faction faction)
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
                straightState.Tags.AddForAll(Tag.AxisControlled);
            else if (controllingTeam == FactionTeam.ALLIES)
                straightState.Tags.AddForAll(Tag.AlliesControlled);
        }
    }

    
    

    public static List<GameStateCalculator> CalculateAll()
    {
        DebugUtilities.PrintPeer("Calculating game state for all factions");
        var calculators = new List<GameStateCalculator>();
        
        foreach (Faction faction in StaticGameData.PlayableFactions)
        {
            calculators.Add(CalculateAllForFaction(faction));
        }

        EventBus.Emit(EventBus.SignalName.GameStateRecalculated);
        return calculators;
    }
    
    public static GameStateCalculator CalculateAllForFaction(Faction faction)
    {
        GameStateCalculator calculator = new GameStateCalculator { Faction = faction };
        
        // Calculate supply first - it's needed by buildable/attackable checks
        CalculateInSupplyForFaction(faction);        
        CalculateAttackableForFaction(faction);
        CalculateBuildableCountriesForFaction(faction);
        CalculateRecruitableCountriesForFaction(faction);
        ApplyCountryTagModifiersForFaction(faction);
        CalculatePlayedCardsForFaction(faction);

        CalculateExecutableStepsForFaction(faction);
        CalculateActivatableCardsForFaction(faction);
        CalculateAfterReactionCardsForFaction(faction);        
        CalculatePlayableCardsForFaction(faction);

        CalculateStraightControlForFaction(faction);

        return calculator;
    }

    private static void ApplyCountryTagModifiersForFaction(Faction faction)
    {
        foreach (ICountryTagModifier modifier in ModifierRegistry.GetAll<ICountryTagModifier>())
            modifier.ApplyTagModifiers(faction);
    }

    private static void ClearTagsForFaction(Faction faction, Tag tag)    {
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
    

}


