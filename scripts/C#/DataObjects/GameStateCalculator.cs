using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Derives every computed tag of a board from its data: supply, what can be attacked, built and
/// recruited, straight control, and which cards are played, activatable and executable. Runs against
/// any <see cref="BoardState"/>; only the live board also replicates the result and announces it.
/// </summary>
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

    private static void CalculateAttackableForFaction(BoardState board, Faction faction)
    {
        ClearTagsForFaction(board, faction, Tag.Attackable);

        List<int> suppliedUnitIds = board.SuppliedUnitIds(faction);
        foreach (int suppliedUnitId in suppliedUnitIds)
        {
            AttackOption attackOption = AttackOption.CalculateAttackOptions(suppliedUnitId, board);
            foreach (UnitState unit in UnitState.ForIds(attackOption.AttackableUnits))
                board.Of(unit).Tags.Add(Tag.Attackable, faction);
            foreach (CountryState country in CountryState.ForIds(attackOption.AttackableCountries))
                board.Of(country).Tags.Add(Tag.Attackable, faction);
        }
    }

    private static void CalculateBuildableCountriesForFaction(BoardState board, Faction faction)
    {
        ClearTagsForFaction(board, faction, Tag.Buildable);

        foreach (var countryState in CountryState.AllCountryStates)
        {
            if (board.CanBuild(faction, countryState))
                board.Of(countryState).Tags.Add(Tag.Buildable, faction);
        }
    }

    private static void CalculateRecruitableCountriesForFaction(BoardState board, Faction faction)
    {
        ClearTagsForFaction(board, faction, Tag.Recruitable);

        foreach (var countryState in CountryState.AllCountryStates)
        {
            if (board.CanRecruit(faction, countryState))
                board.Of(countryState).Tags.Add(Tag.Recruitable, faction);
        }
    }

    private static void CalculateActivatableCardsForFaction(GameSituation situation, Faction faction)
    {
        BoardState board = situation.Board;
        ClearTagsForFaction(board, faction, Tag.IsActivatable);

        FactionRecord piles = board.ForFaction(faction);
        List<CardState> candidates = new();
        candidates.AddRange(CardState.ForIds(piles.Hand));
        candidates.AddRange(CardState.ForIds(piles.Status));
        candidates.AddRange(CardState.ForIds(piles.Response));

        List<CardState> cardPlayedThisTurn =
            CardState.AllForFaction(faction).Values
                .Where(cs => board.Of(cs).PlayedInTurn.Contains(board.GameTurn))
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

            CardRecord card = board.Of(cardState);
            bool canActivate = cardState.CardLogic.CanBeActivated(situation);
            if (canActivate)
            {
                card.Tags.Add(Tag.IsActivatable, faction);
            }

            if (card.IsBlocked)
                card.Tags.Add(Tag.IsBlocked, faction);
            else
                card.Tags.Remove(Tag.IsBlocked, faction);
        };
    }

    private static void CalculatePlayedCardsForFaction(GameSituation situation, Faction faction)
    {
        BoardState board = situation.Board;
        ClearTagsForFaction(board, faction, Tag.IsPlayed);

        FactionRecord piles = board.ForFaction(faction);
        void MarkPlayed(IEnumerable<CardState> cards) { foreach (CardState card in cards) board.Of(card).Tags.Add(Tag.IsPlayed, faction); }

        // STATUS and RESPONSE cards are played once they are in their permanent piles
        MarkPlayed(CardState.ForIds(piles.Status));
        MarkPlayed(CardState.ForIds(piles.Response));

        // EVENT cards are played once discarded
        MarkPlayed(CardState.ForIds(piles.Discarded));

        // EVENT cards currently in the active play round pool are also considered played
        // (covers the window between entering the pool and being moved to discard)
        MarkPlayed(situation.CardPool.Where(cardState => cardState.Faction == faction));
    }

    private static void CalculateAfterReactionCardsForFaction(BoardState board, Faction faction)
    {
        ClearTagsForFaction(board, faction, Tag.IsAfterReaction);
        CardState.AllForFaction(faction).Values.ToList().ForEach(cardState =>
        {
            // IsPlayed: a card can only be used as a reaction once it is on the table. A hand card
            // tagged IsActivatable is offering a *play*, not a reaction.
            if (board.IsPlayed(cardState) && board.Of(cardState).Tags.Has(Tag.IsActivatable, faction) && cardState.CardLogic?.IsBlockReaction == false)
                board.Of(cardState).Tags.Add(Tag.IsAfterReaction, faction);
        });
    }

    /// <summary>
    /// Mirror of <see cref="CalculateAfterReactionCardsForFaction"/> for block reactions: an activatable
    /// card whose triggers include <see cref="Condition.IsBlockRequest"/>. Consumed server-side by
    /// CardPlayRound.GetBlockReactionOptions — block cards are excluded from Tag.IsAfterReaction, so
    /// without this tag they have no route to ever be offered.
    /// </summary>
    private static void CalculateBlockReactionCardsForFaction(BoardState board, Faction faction)
    {
        ClearTagsForFaction(board, faction, Tag.IsBlockReaction);
        CardState.AllForFaction(faction).Values.ToList().ForEach(cardState =>
        {
            if (board.IsPlayed(cardState) && board.Of(cardState).Tags.Has(Tag.IsActivatable, faction) && cardState.CardLogic?.IsBlockReaction == true)
                board.Of(cardState).Tags.Add(Tag.IsBlockReaction, faction);
        });
    }

    private static void CalculatePlayableCardsForFaction(BoardState board, Faction faction)
    {
        ClearTagsForFaction(board, faction, Tag.IsPlayable);
        foreach (CardState cardState in CardState.ForIds(board.ForFaction(faction).Hand))
            if (board.Of(cardState).Tags.Has(Tag.IsActivatable, faction) && !board.IsPlayed(cardState))
                board.Of(cardState).Tags.Add(Tag.IsPlayable, faction);
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
    private static void CalculateAttentionCardsForFaction(GameSituation situation, Faction faction)
    {
        BoardState board = situation.Board;
        ClearTagsForFaction(board, faction, Tag.NeedsAttention);
        CardState.AllForFaction(faction).Values.ToList().ForEach(cardState =>
        {
            if (!board.Of(cardState).Tags.Has(Tag.IsActivatable, faction))
                return;
            // A Status/Response card in hand is being PLAYED onto the table; its steps are the later
            // activation effect and say nothing about this play. Same reasoning as
            // CardLogic.IsTableCardInHand. The != false also covers a null CardLogic.
            if (cardState.CardLogic?.IsTableCardInHand != false)
                return;

            List<CardStep> steps = cardState.CardLogic.CardSteps
                .Where(step => board.Of(step).Tags.HasForAny(Tag.IsExecutable)).ToList();
            if (steps.Count == 0)
                return;
            if (steps.All(step => !step.MeetAllAdvisoryConditions(situation)))
                board.Of(cardState).Tags.Add(Tag.NeedsAttention, faction);
        });
    }

    private static void CalculateInSupplyForFaction(BoardState board, Faction faction)
    {
        ClearTagsForFaction(board, faction, Tag.InSupply);
        ClearTagsForFaction(board, faction, Tag.OutOfSupply);

        var pathFindingService = new PathFindingService(new PathFindingNodeDefault(), faction, board);
        List<UnitState> activeUnits = UnitState.ForIds(board.ActiveUnitIds(faction));

        foreach (UnitState unit in activeUnits)
        {
            UnitRecord record = board.Of(unit);
            bool modifierGrantsSupply = board.Modifiers<IUnitSupplyModifier>()
                .Any(m => m.GrantsSupply(board, unit));

            if (modifierGrantsSupply || record.SuppliedForTurn || CalculateSupplyForUnit(board, pathFindingService, unit, faction))
            {
                record.Tags.Add(Tag.InSupply, faction);
            }
            else
            {
                record.Tags.Add(Tag.OutOfSupply, faction);
            }
        }
    }

    private static bool CalculateSupplyForUnit(BoardState board, PathFindingService pathFinding, UnitState unit, Faction faction)
    {
        int countryId = board.CountryOf(unit);
        foreach (var supplyId in board.SupplyCountryIds(faction))
        {
            if (pathFinding.CalculatePath(countryId, supplyId) >= 0)
            {
                if (unit.IsNavy)
                    return board.HasHarbor(faction, CountryState.ForId(countryId));
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
    private static bool[] EvaluateExecutableSteps(GameSituation situation, List<CardStep> steps)
    {
        BoardState board = situation.Board;
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
            foreach (int cardId in board.ForFaction(faction).Deck)
                inDrawPile.Add(cardId);

        bool[] executable = new bool[steps.Count];
        System.Threading.Tasks.Parallel.For(0, steps.Count, i =>
        {
            CardStep step = steps[i];
            executable[i] = !inDrawPile.Contains(step.CardLogic.CardState.Id)
                            && !step.StepFinished
                            && step.MeetAllConditions(situation);
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
    private static void ApplyExecutableStepTags(BoardState board, List<CardStep> steps, bool[] executable, Faction faction)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            TagContainer tags = board.Of(steps[i]).Tags;
            if (executable[i]) tags.Add(Tag.IsExecutable, faction);
            else               tags.Remove(Tag.IsExecutable, faction);
        }
    }

    private static void CalculateStraightControlForFaction(BoardState board)
    {
        // Clear old straight control tags
        foreach (var straightState in GameSession.Current.GameState.StraightStates)
        {
            board.Of(straightState).Tags.RemoveForAll(Tag.AxisControlled);
            board.Of(straightState).Tags.RemoveForAll(Tag.AlliesControlled);
        }

        // Calculate control for each straight based on controlling country's team
        foreach (var straightState in GameSession.Current.GameState.StraightStates)
        {
            FactionTeam controllingTeam = board.OccupyingTeam(straightState.ControllingCountryState);

            if (controllingTeam == FactionTeam.AXIS)
                board.Of(straightState).Tags.AddForAll(Tag.AxisControlled);
            else if (controllingTeam == FactionTeam.ALLIES)
                board.Of(straightState).Tags.AddForAll(Tag.AlliesControlled);
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

    /// <summary>
    /// Re-derive the live board's tag state. <paramref name="scope"/> names which sources of truth
    /// moved; see <see cref="RecalcScope"/> for why the default is everything.
    /// </summary>
    public static void CalculateAll(RecalcScope scope = RecalcScope.All) => CalculateAll(scope, BoardState.Live);

    /// <summary>
    /// Re-derive <paramref name="board"/>'s tag state. A forked board is computed in full and in
    /// silence: the Enabled switch, the server-only rule, replication and the recalculated signal are
    /// all about the live game, and none of them apply to a board nobody else can see.
    /// </summary>
    public static void CalculateAll(RecalcScope scope, BoardState board) =>
        CalculateAll(scope, GameSituation.Live.WithBoard(board));

    /// <summary>
    /// As above, with the conditions that feed the card and step tags evaluated in <paramref name="situation"/>
    /// — for a fork that is reacting to a hypothetical event, whose pool and trigger those conditions read.
    /// </summary>
    public static void CalculateAll(RecalcScope scope, GameSituation situation)
    {
        if (scope == RecalcScope.None) return;

        BoardState board = situation.Board;
        if (!board.IsLive)
        {
            CalculatePhases(scope, situation);
            return;
        }

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
            DebugUtilities.PrintPeer($"Calculating game state for all factions (scope {scope})");
            CalculatePhases(scope, situation);

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
                CalculateStraightControlForFaction(board);
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
    /// The derivation itself. Phases rather than six independent per-faction passes, because the step
    /// evaluation in the middle is shared. The ordering — board -> played cards -> executable steps ->
    /// card tags — is a real dependency chain, not a convention:
    ///
    ///   board       feeds the step conditions (HasBuildableLand and friends read Tag.Buildable)
    ///   playedCards feeds them too (Condition.CardIsPlayed reads Tag.IsPlayed)
    ///   steps       feed the card tags (CanBeActivated consults HasExecutableCardSteps)
    ///
    /// That chain is why `scope` only says which SOURCE moved: everything downstream of it has to be
    /// redone regardless, so only the two independent heads are actually optional. Splitting the
    /// phases also makes it deterministic: six factions running the whole sequence in parallel let one
    /// faction's step conditions read tags another faction's thread was still writing.
    /// </summary>
    private static void CalculatePhases(RecalcScope scope, GameSituation situation)
    {
        BoardState board = situation.Board;
        if (scope.HasFlag(RecalcScope.Board))
        {
            System.Threading.Tasks.Parallel.ForEach(StaticGameData.PlayableFactions,
                faction => CalculateBoardTagsForFaction(board, faction));

            // Straight control writes global (non-faction-scoped) tags — run once after parallel
            // work, and before the conditions below, which may read them.
            CalculateStraightControlForFaction(board);
        }

        if (scope.HasFlag(RecalcScope.Decks))
            System.Threading.Tasks.Parallel.ForEach(StaticGameData.PlayableFactions,
                faction => CalculatePlayedCardsForFaction(situation, faction));

        // Downstream of both heads, so it runs whenever anything at all moved.
        if (scope != RecalcScope.None)
        {
            // Once for everyone. See EvaluateExecutableSteps for why this is not per faction.
            List<CardStep> steps = CardStep.All;
            bool[] executable = EvaluateExecutableSteps(situation, steps);
            foreach (Faction faction in StaticGameData.PlayableFactions)
                ApplyExecutableStepTags(board, steps, executable, faction);

            System.Threading.Tasks.Parallel.ForEach(StaticGameData.PlayableFactions,
                faction => CalculateCardTagsForFaction(situation, faction));
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
        CalculateStraightControlForFaction(BoardState.Live);

        EventBus.Emit(EventBus.SignalName.GameStateRecalculated);
    }

    /// <summary>
    /// Board-derived tags: supply, and what this faction may attack, build and recruit into. Depends
    /// only on unit positions and strait control, so it must run before anything that reads those
    /// tags — notably CardStep conditions such as <c>HasBuildableLand</c>, which read Tag.Buildable.
    ///
    /// CalculatePlayedCardsForFaction deliberately does NOT live here, though it used to: it reads
    /// deck piles and the play pool, not the board, and leaving it in this group would have forced
    /// every draw, discard and play — about a quarter of all ChangeEvents — to re-derive supply and
    /// buildability for six factions to answer a question about which cards are on the table.
    /// </summary>
    private static void CalculateBoardTagsForFaction(BoardState board, Faction faction)
    {
        // Supply first — buildable/attackable both consult it.
        CalculateInSupplyForFaction(board, faction);
        CalculateAttackableForFaction(board, faction);
        CalculateBuildableCountriesForFaction(board, faction);
        CalculateRecruitableCountriesForFaction(board, faction);
        ApplyCountryTagModifiersForFaction(board, faction);
    }

    /// <summary>
    /// Card-derived tags. Runs after <see cref="Tag.IsExecutable"/> is settled, because
    /// CalculateActivatableCardsForFaction resolves CardLogic.CanBeActivated, which consults
    /// HasExecutableCardSteps — i.e. the executable tags stamped immediately before it.
    /// </summary>
    private static void CalculateCardTagsForFaction(GameSituation situation, Faction faction)
    {
        CalculateActivatableCardsForFaction(situation, faction);
        CalculateAfterReactionCardsForFaction(situation.Board, faction);
        CalculateBlockReactionCardsForFaction(situation.Board, faction);
        CalculatePlayableCardsForFaction(situation.Board, faction);
        CalculateAttentionCardsForFaction(situation, faction);
    }

    private static void ApplyCountryTagModifiersForFaction(BoardState board, Faction faction)
    {
        foreach (ICountryTagModifier modifier in board.Modifiers<ICountryTagModifier>().Where(m => m.Faction == faction))
            modifier.ApplyTagModifiers(board, faction);
    }

    private static void ClearTagsForFaction(BoardState board, Faction faction, Tag tag)
    {
        foreach (UnitRecord unit in board.UnitRecords)
            unit.Tags.Remove(tag, faction);
        foreach (CountryRecord country in board.CountryRecords)
            country.Tags.Remove(tag, faction);
        foreach (CardRecord card in board.CardRecords)
            card.Tags.Remove(tag, faction);
    }
}
