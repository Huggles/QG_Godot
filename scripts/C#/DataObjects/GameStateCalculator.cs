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
        Tag.IsActivatable, Tag.IsPlayable, Tag.IsAfterReaction, Tag.IsPlayed, Tag.IsBlocked,
        Tag.NeedsAttention
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

    /// <summary>
    /// Raises <see cref="Tag.NeedsAttention"/> on a card that can be used but whose every executable
    /// step would be hollow — see <see cref="CardStep.WithAdvisoryCondition"/>. The card stays
    /// activatable and selectable; only the way it is drawn changes.
    ///
    /// All(), not Any(): a step with no advisory condition always meets it, so a card offering one
    /// hollow effect beside one real effect is left alone. Runs after
    /// <see cref="CalculateActivatableCardsForFaction"/>, whose tag it reads.
    /// </summary>
    private static void CalculateAttentionCardsForFaction(Faction faction)
    {
        ClearTagsForFaction(faction, Tag.NeedsAttention);
        CardState.AllForFaction(faction).Values.ToList().ForEach(cardState =>
        {
            if (!cardState.HasTag(Tag.IsActivatable, faction))
                return;
            // A Status/Response card in hand is being PLAYED onto the table; its steps are the later
            // activation effect and say nothing about this play. Same reasoning as
            // CardLogic.IsTableCardInHand. The != false also covers a null CardLogic.
            if (cardState.CardLogic?.IsTableCardInHand != false)
                return;

            List<CardStep> steps = cardState.CardLogic.ExecutableCardSteps;
            if (steps.Count == 0)
                return;
            if (steps.All(step => !step.MeetAllAdvisoryConditions))
                cardState.AddTag(Tag.NeedsAttention, faction);
        });
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

    /// <summary>
    /// Which steps could run right now. Evaluated ONCE per pass, not once per faction.
    ///
    /// <see cref="CardStep.MeetAllConditions"/> does not depend on who is asking: a step's conditions
    /// are built from its own card's faction, so all six factions were computing the identical answer
    /// and throwing five copies away. That duplication was ~77% of all CalculateAll time — invisible
    /// in wall-clock because the six copies ran in parallel with each other, which is exactly how it
    /// survived this long.
    ///
    /// Parallel over STEPS rather than over factions, so the one remaining copy still uses the cores
    /// the five redundant ones were using. <c>CardStep.All</c> is materialised once because it is a
    /// property that rebuilds the whole list from every CardState on every access, and the old code
    /// touched it twice per faction — about 13,000 rebuilds in a full game.
    /// </summary>
    private static bool[] EvaluateExecutableSteps(List<CardStep> steps)
    {
        // Cards still face-down in a draw pile are skipped rather than evaluated. Nothing can activate
        // one — CalculateActivatableCardsForFaction only ever considers hand, status, response,
        // played-this-turn and mutator cards, and CardPlayRound only asks about cards in its pool — so
        // their conditions are computed and thrown away. They are the majority of the deck for most of
        // a game, and MeetAllConditions is the single most expensive thing here.
        //
        // Skipped means FALSE, not "left alone": clearing keeps the tag well defined, so a card
        // shuffled back into the deck cannot carry a stale IsExecutable from when it was last in play.
        HashSet<int> inDrawPile = new();
        foreach (Faction faction in StaticGameData.PlayableFactions)
            foreach (int cardId in DeckState.ForFaction(faction).DeckCardIds)
                inDrawPile.Add(cardId);

        bool[] executable = new bool[steps.Count];
        System.Threading.Tasks.Parallel.For(0, steps.Count, i =>
        {
            CardStep step = steps[i];
            executable[i] = !inDrawPile.Contains(step.CardLogic.CardState.Id)
                            && !step.StepFinished
                            && step.MeetAllConditions;
        });
        return executable;
    }

    /// <summary>
    /// Stamp one faction's copy of <see cref="Tag.IsExecutable"/> from a shared evaluation.
    ///
    /// The per-faction dimension of this tag carries no information — every faction gets the same
    /// answer — but it is preserved rather than collapsed because the tag state stays byte-identical
    /// to what the per-faction loop produced. The only reader, <c>CardLogic.ExecutableCardSteps</c>,
    /// asks <c>HasTagForAny</c> and never names a faction.
    /// </summary>
    private static void ApplyExecutableStepTags(List<CardStep> steps, bool[] executable, Faction faction)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            if (executable[i]) steps[i].Tags.Add(Tag.IsExecutable, faction);
            else               steps[i].Tags.Remove(Tag.IsExecutable, faction);
        }
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

    
    

    /// <summary>
    /// Whether any OTHER peer needs to be told about derived state.
    ///
    /// False for a solo GUI game and for every headless/CLI run, which use OfflineMultiplayerPeer:
    /// it reports unique id 1 so IsServer() is true, but GetPeers() is empty. False is therefore
    /// "authoritative and alone", not "not started" — a real host with clients has a non-empty
    /// GetPeers() from the readiness barrier onward, i.e. well before the first CalculateAll.
    ///
    /// Null peer means the session is not up yet; nothing to send to either.
    /// </summary>
    private static bool HasRemotePeers
        => MultiplayerSession.Instance?.Multiplayer?.MultiplayerPeer != null
           && MultiplayerSession.Instance.Multiplayer.GetPeers().Length > 0;

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
            // Three phases rather than six independent per-faction passes, because the middle one is
            // shared. The ordering board tags -> executable steps -> card tags is the same order a
            // single faction's pass used, so each phase still sees what it used to.
            //
            // It is also the order the old code only APPEARED to have. Six factions ran the whole
            // sequence in parallel, so one faction's CardStep conditions (which read Tag.Buildable)
            // could be evaluated while another faction's thread was still writing those very tags —
            // the answer depended on thread scheduling. Splitting the phases makes it deterministic.
            System.Threading.Tasks.Parallel.ForEach(StaticGameData.PlayableFactions,
                CalculateBoardTagsForFaction);

            // Straight control writes global (non-faction-scoped) tags — run once after parallel work,
            // and before the conditions below, which may read them.
            CalculateStraightControlForFaction();

            // Once for everyone. See EvaluateExecutableSteps for why this is not per faction.
            List<CardStep> steps = CardStep.All;
            bool[] executable = EvaluateExecutableSteps(steps);
            foreach (Faction faction in StaticGameData.PlayableFactions)
                ApplyExecutableStepTags(steps, executable, faction);

            System.Threading.Tasks.Parallel.ForEach(StaticGameData.PlayableFactions,
                CalculateCardTagsForFaction);

            // Build the snapshot, apply it locally, and replicate it to clients as an ordered
            // RecalculateTagsMessage. Because ChangeEvent.Apply() broadcasts the change
            // event BEFORE calling CalculateAll(), this tags message is enqueued on clients right
            // behind that change event and always applies to post-change state in queue order.
            // The snapshot exists ONLY to reach clients. On the server it is a round trip:
            // BuildTagsSnapshot reads back the tags CalculateAllForFaction has just written, and
            // ApplyComputedTags clears every replicated tag and writes those same values in again.
            // With nobody to send it to, both halves are pure cost — and this runs after every
            // ChangeEvent (~1100 times in a full game), so it dominates an automated run.
            //
            // Gated on the absence of REMOTE peers rather than on a CLI/sim flag: the saving is just
            // as real for a solo GUI game, and the condition is the honest statement of why it is
            // safe. Nothing else consumes the snapshot — RecalculateTagsMessage is not recorded in
            // GameMessages, not counted in LatestAppliedId, not hashed, and not written to saves.
            if (HasRemotePeers)
            {
                var snapshot = BuildTagsSnapshot();
                ApplyComputedTags(snapshot);
                _ = new RecalculateTagsMessage(snapshot).BroadCast();
            }
            else
            {
                // The two things ApplyComputedTags does that are NOT the round trip, so a peerless
                // server behaves identically to one with clients.
                CalculateStraightControlForFaction();
                EventBus.Emit(EventBus.SignalName.GameStateRecalculated);
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
        List<CardStep> steps = CardStep.All;

        CalculateBoardTagsForFaction(faction);
        ApplyExecutableStepTags(steps, EvaluateExecutableSteps(steps), faction);
        CalculateCardTagsForFaction(faction);

        return calculator;
    }

    /// <summary>
    /// Board-derived tags: supply, and what this faction may attack, build and recruit into. Depends
    /// only on unit positions, so it must run before anything that reads those tags — notably
    /// CardStep conditions such as <c>HasBuildableLand</c>, which read Tag.Buildable.
    /// </summary>
    private static void CalculateBoardTagsForFaction(Faction faction)
    {
        // Supply first — buildable/attackable both consult it.
        CalculateInSupplyForFaction(faction);
        CalculateAttackableForFaction(faction);
        CalculateBuildableCountriesForFaction(faction);
        CalculateRecruitableCountriesForFaction(faction);
        ApplyCountryTagModifiersForFaction(faction);
        CalculatePlayedCardsForFaction(faction);
    }

    /// <summary>
    /// Card-derived tags. Runs after <see cref="Tag.IsExecutable"/> is settled, because
    /// CalculateActivatableCardsForFaction resolves CardLogic.CanBeActivated, which consults
    /// HasExecutableCardSteps — i.e. the executable tags stamped immediately before it.
    /// </summary>
    private static void CalculateCardTagsForFaction(Faction faction)
    {
        CalculateActivatableCardsForFaction(faction);
        CalculateAfterReactionCardsForFaction(faction);
        CalculateBlockReactionCardsForFaction(faction);
        CalculatePlayableCardsForFaction(faction);
        CalculateAttentionCardsForFaction(faction);
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


