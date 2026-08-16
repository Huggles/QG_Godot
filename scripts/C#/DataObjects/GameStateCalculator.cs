using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public class GameStateCalculator
{
    /// <summary>
    /// Tags computed by this calculator that are replicated to clients.
    /// CardStep.IsExecutable is intentionally excluded — it is server-internal execution state.
    /// Tags.Clickable is excluded — it is set by input handlers locally.
    /// Tag.IsBlockReaction is excluded — it is server-internal. The block-eligible card ids are sent
    /// to the controlling peer explicitly as InputRequest.TargetCardIds by CardPlayRound.RequestBlock,
    /// so replicating the tag would be redundant. Do not add it here.
    /// </summary>
    public static readonly HashSet<Tag> ReplicatedTags = new()
    {
        // Country
        Tag.Attackable, Tag.Buildable, Tag.Recruitable,
        // Unit
        Tag.InSupply, Tag.OutOfSupply,
        // Card
        Tag.IsActivatable, Tag.IsPlayable, Tag.IsAfterReaction, Tag.IsPlayed, Tag.IsBlocked
        // Straight tags (AxisControlled / AlliesControlled) are intentionally excluded:
        // they are 100% deterministic from ControllingCountryId and are recomputed
        // locally via CalculateStraightControlForFaction() on every peer.
    };

    public static bool Enabled {
        get { return field; }
        set { 
            field = value;
            if(field) CalculateAll();
        }
    } = true;
    

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

        List<CardState> cardPlayedThisTurn = 
            CardState.AllForFaction(faction).Values
                .Where(cs => cs.PlayedInTurn.Contains(GameFlow.Instance.GameTurn))
                .ToList();
        candidates.AddRange(cardPlayedThisTurn);

        // Activatable scenario mutators are in no DeckState pile and are never "played", so none of the
        // sources above can reach them. Everything downstream of Tag.IsActivatable then applies as-is.
        candidates.AddRange(CardState.AllForFaction(faction).Values.Where(cs => cs.CardLogic is ActivatableMutator));

        candidates = candidates.Distinct().ToList();
        

        foreach (var cardState in candidates)
        {
            if (cardState.CardLogic == null) { 
                DebugUtilities.PrintPeer($"[DIAG]   {cardState.CardData?.UniqueName ?? "?"} skipped - CardLogic is null"); 
                continue; 
            }

            bool canActivate = cardState.CardLogic.CanBeActivated();
            if (canActivate)
            {   
                cardState.AddTag(Tag.IsActivatable, faction);
            }

            if (cardState.IsBlocked)
                cardState.AddTag(Tag.IsBlocked, faction);
            else
                cardState.Tags.Remove(Tag.IsBlocked, faction);
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
            // IsPlayed: a card can only be used as a reaction once it is on the table. A hand card
            // tagged IsActivatable is offering a *play*, not a reaction.
            if (cardState.IsPlayed && cardState.HasTag(Tag.IsActivatable, faction) && cardState.CardLogic?.IsBlockReaction == false)
                cardState.AddTag(Tag.IsAfterReaction, faction);
        });
    }

    /// <summary>
    /// Mirror of <see cref="CalculateAfterReactionCardsForFaction"/> for block reactions: an activatable
    /// card whose triggers include <see cref="Condition.IsBlockRequest"/>. Consumed server-side by
    /// CardPlayRound.GetBlockReactionOptions — block cards are excluded from Tag.IsAfterReaction, so
    /// without this tag they have no route to ever be offered.
    /// </summary>
    private static void CalculateBlockReactionCardsForFaction(Faction faction)
    {
        ClearTagsForFaction(faction, Tag.IsBlockReaction);
        CardState.AllForFaction(faction).Values.ToList().ForEach(cardState =>
        {
            if (cardState.IsPlayed && cardState.HasTag(Tag.IsActivatable, faction) && cardState.CardLogic?.IsBlockReaction == true)
                cardState.AddTag(Tag.IsBlockReaction, faction);
        });
    }

    private static void CalculatePlayableCardsForFaction(Faction faction)
    {
        ClearTagsForFaction(faction, Tag.IsPlayable);        
        DeckState deckState = DeckState.ForFaction(faction);
        deckState.HandCardStates
            .Where(cs => cs.HasTag(Tag.IsActivatable, faction) && !cs.IsPlayed)
            .AddTag(Tag.IsPlayable, faction);
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
            bool modifierGrantsSupply = ModifierRegistry.GetAll<IUnitSupplyModifier>()
                .Any(m => m.GrantsSupply(unit));

            if (modifierGrantsSupply || unit.SuppliedForTurn || CalculateSupplyForUnit(pathFindingService, unit.Id, faction))
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
            if (pathFinding.CalculatePath(unit.CountryId, supplyId) >= 0)
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
            bool isExecutable = !step.StepFinished && step.MeetAllConditions;
            if (isExecutable)
                step.Tags.Add(Tag.IsExecutable, faction);
        });
    }

    private static void CalculateStraightControlForFaction()
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

    
    

    public static void CalculateAll()
    {
        if (!Enabled)
        {
            DebugUtilities.PrintPeer("[SKIP] GameStateCalculator is disabled");
            return;
        }

        // Tag calculation is server-authoritative. Clients receive computed tags via the
        // RecalculateTagsMessage broadcast after each ChangeEvent and never recalculate independently.
        if (MultiplayerSession.Instance != null && !MultiplayerSession.Instance.Multiplayer.IsServer())
        {
            DebugUtilities.PrintPeer("[SKIP] GameStateCalculator.CalculateAll is server-only — awaiting tags from server");
            return;
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            DebugUtilities.PrintPeer("Calculating game state for all factions");
            var calculators = new System.Collections.Concurrent.ConcurrentBag<GameStateCalculator>();
            
            System.Threading.Tasks.Parallel.ForEach(StaticGameData.PlayableFactions, faction =>
            {
                calculators.Add(CalculateAllForFaction(faction));
            });

            // Straight control writes global (non-faction-scoped) tags — run once after parallel work
            CalculateStraightControlForFaction();

            // Build the snapshot, apply it locally, and replicate it to clients as an ordered
            // RecalculateTagsMessage. Because ChangeEvent.Apply() broadcasts the change
            // event BEFORE calling CalculateAll(), this tags message is enqueued on clients right
            // behind that change event and always applies to post-change state in queue order.
            var snapshot = BuildTagsSnapshot();
            ApplyComputedTags(snapshot);

            if (MultiplayerSession.Instance?.Multiplayer.IsServer() == true)
            {
                _ = new RecalculateTagsMessage(snapshot).BroadCast();
            }

            stopwatch.Stop();
            DebugUtilities.PrintPeer($"[TIMING] CalculateAll completed in {stopwatch.ElapsedMilliseconds}ms");
            return;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            DebugUtilities.PrintPeer($"[TIMING] CalculateAll FAILED in {stopwatch.ElapsedMilliseconds}ms");
            DebugUtilities.PrintPeer($"[ERROR] Exception during game state calculation after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}");
            DebugUtilities.PrintPeer($"[ERROR] Stack Trace: {ex.StackTrace}");
            throw;
        }        
    }

    /// <summary>
    /// Serializes all replicated tags from live state objects into a snapshot for wire transmission.
    /// Called on the server after every CalculateAll().
    /// </summary>
    public static ComputedTagsSnapshot BuildTagsSnapshot()
    {
        var entries = new List<TagEntry>();
        var state   = GameSession.Current.GameState;

        foreach (var cs in state.CountryStateById.Values)
            foreach (var tag in ReplicatedTags)
                foreach (var faction in cs.Tags.GetFactionsWithTag(tag))
                    entries.Add(new TagEntry { ObjectType = "Country", Id = cs.Id, Tag = tag, Faction = faction });

        foreach (var us in state.UnitStatesById.Values)
            foreach (var tag in ReplicatedTags)
                foreach (var faction in us.Tags.GetFactionsWithTag(tag))
                    entries.Add(new TagEntry { ObjectType = "Unit", Id = us.Id, Tag = tag, Faction = faction });

        foreach (var card in state.CardStatesById.Values)
            foreach (var tag in ReplicatedTags)
                foreach (var faction in card.Tags.GetFactionsWithTag(tag))
                    entries.Add(new TagEntry { ObjectType = "Card", Id = card.Id, Tag = tag, Faction = faction });

        return new ComputedTagsSnapshot { Entries = entries };
    }

    /// <summary>
    /// Clears all replicated tags from all state objects and re-applies them from the snapshot.
    /// Called on the server (after CalculateAll), on clients (via RecalculateTagsMessage), and on resync.
    /// Emits GameStateRecalculated when done.
    /// </summary>
    public static void ApplyComputedTags(ComputedTagsSnapshot snapshot)
    {
        var state = GameSession.Current.GameState;

        // Clear all replicated tags before re-applying
        foreach (var cs in state.CountryStateById.Values)
            foreach (var tag in ReplicatedTags)
                cs.Tags.RemoveForAll(tag);

        foreach (var us in state.UnitStatesById.Values)
            foreach (var tag in ReplicatedTags)
                us.Tags.RemoveForAll(tag);

        foreach (var card in state.CardStatesById.Values)
            foreach (var tag in ReplicatedTags)
                card.Tags.RemoveForAll(tag);

        // Apply entries from snapshot
        foreach (var entry in snapshot.Entries)
        {
            switch (entry.ObjectType)
            {
                case "Country":
                    if (state.CountryStateById.TryGetValue(entry.Id, out var cs))
                        cs.Tags.Add(entry.Tag, entry.Faction);
                    break;
                case "Unit":
                    if (state.UnitStatesById.TryGetValue(entry.Id, out var us))
                        us.Tags.Add(entry.Tag, entry.Faction);
                    break;
                case "Card":
                    if (state.CardStatesById.TryGetValue(entry.Id, out var card))
                        card.Tags.Add(entry.Tag, entry.Faction);
                    break;
            }
        }

        // Straight control is deterministic from ControllingCountryId — recompute locally
        // on every peer rather than including it in the snapshot.
        CalculateStraightControlForFaction();

        EventBus.Emit(EventBus.SignalName.GameStateRecalculated);
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
        CalculateBlockReactionCardsForFaction(faction);
        CalculatePlayableCardsForFaction(faction);

        return calculator;
    }

    private static void ApplyCountryTagModifiersForFaction(Faction faction)
    {
        foreach (ICountryTagModifier modifier in ModifierRegistry.GetAll<ICountryTagModifier>().Where(m => m.Faction == faction))
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


