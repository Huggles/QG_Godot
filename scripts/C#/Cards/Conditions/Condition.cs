using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

/// <summary>Controls whether an <see cref="Condition.EventCondition"/> checks the full event pool or only the current reaction window trigger.</summary>
public enum ConditionScope { Pool, Immediate }

[DebuggerDisplay("Condition: {GetType().Name}: {_meetsCondition}")]
public abstract class Condition
{
    private bool _meetsCondition => MeetCondition();
    public abstract bool MeetCondition();
    public CardLogic CardLogic;

    /// <summary>
    /// True when this condition only makes sense in the context of an active change-event
    /// (i.e. it inspects the change-event pool, the current block target, or is a block marker).
    /// Used by <see cref="CardLogic.HasEventBasedTrigger"/> to decide whether a card may
    /// activate inside a reaction chain.
    /// </summary>
    public virtual bool RequiresEventContext => false;

    List<int> CountryIds;
    List<CountryState> CountryStates => CountryState.ForIds(CountryIds);
    CountryState CountryState => CountryStates[0];

    List<int> UnitIds;
    List<UnitState> UnitStates => UnitState.ForIds(UnitIds);
    UnitState UnitState => UnitStates[0];


    List<int> CardIds;
    List<CardState> CardStates => CardState.ForIds(CardIds);
    CardState CardState => CardStates[0];

    Faction Faction;
    FactionTeam FactionTeam;

    Faction TargetFaction;    
    List<Faction> TargetFactions;
    
    FactionTeam TargetFactionTeam;


    TurnStep TurnStep;
    DeployType DeployType;
    UnitType UnitType;
    CountryType CountryType;
    CardType? TargetCardType;

    public Condition() { }

    public static T Build<T>(T CardTrigger, CardLogic cardLogic) where T : Condition
    {
        CardTrigger.CardLogic = cardLogic;
        return CardTrigger;
    }

    public class Always : Condition
    {
        public override bool MeetCondition() { return true; }
    }
    public class Never : Condition
    {
        public override bool MeetCondition() { return false; }
    }
    public class Not : CustomCondition
    {
        public Condition Inner { get; }
        public Not(Condition condition) : base(() => !condition.MeetCondition()) { Inner = condition; }
    }

    public class CardIsPlayed : Condition
    {
        private readonly CardState _cardState;
        public CardIsPlayed(CardState cardState) { _cardState = cardState; }
        public override bool MeetCondition() => _cardState.IsPlayed;
    }

    public class CardHasBeenPlayedInTurn : Condition
    {
        private readonly CardState _cardState;
        private readonly int _turn;
        public CardHasBeenPlayedInTurn(CardState cardState, int turn) { _cardState = cardState; _turn = turn; }
        public override bool MeetCondition() => _cardState.PlayedInTurn.Contains(_turn);
    }

    public class CardHasBeenPlayedThisTurn : Condition
    {
        private readonly CardState _cardState;
        public CardHasBeenPlayedThisTurn(CardState cardState) { _cardState = cardState; }
        public override bool MeetCondition() => _cardState.PlayedInTurn.Contains(GameFlow.Instance.GameTurn);
    }

    public class CardHasNotBeenPlayedInTurn : Condition
    {
        private readonly CardState _cardState;
        private readonly int _turn;
        public CardHasNotBeenPlayedInTurn(CardState cardState, int turn) { _cardState = cardState; _turn = turn; }
        public override bool MeetCondition() => !_cardState.PlayedInTurn.Contains(_turn);
    }

    public class CardHasNotBeenPlayedThisTurn : Condition
    {
        private readonly CardState _cardState;
        public CardHasNotBeenPlayedThisTurn(CardState cardState) { _cardState = cardState; }
        public override bool MeetCondition() =>
            !_cardState.PlayedInTurn.Contains(GameFlow.Instance.GameTurn);
    }

    public class CardHasBeenActivatedInTurn : Condition
    {
        private readonly CardState _cardState;
        private readonly int _turn;
        public CardHasBeenActivatedInTurn(CardState cardState, int turn) { _cardState = cardState; _turn = turn; }
        public override bool MeetCondition() => _cardState.ActivatedInTurns.Contains(_turn);
    }

    public class CardHasBeenActivatedThisTurn : Condition
    {
        private readonly CardState _cardState;
        public CardHasBeenActivatedThisTurn(CardState cardState) { _cardState = cardState; }
        public override bool MeetCondition() => _cardState.ActivatedInTurns.Contains(GameFlow.Instance.GameTurn);
    }

    public class CardHasNotBeenActivatedInTurn : Condition
    {
        private readonly CardState _cardState;
        private readonly int _turn;
        public CardHasNotBeenActivatedInTurn(CardState cardState, int turn) { _cardState = cardState; _turn = turn; }
        public override bool MeetCondition() => !_cardState.ActivatedInTurns.Contains(_turn);
    }

    public class CardHasNotBeenActivatedThisTurn : Condition
    {
        private readonly CardState _cardState;
        public CardHasNotBeenActivatedThisTurn(CardState cardState) { _cardState = cardState; }
        public override bool MeetCondition() => !_cardState.ActivatedInTurns.Contains(GameFlow.Instance.GameTurn);
    }

    public class CountryIsBuildable : Condition
    {
        public CountryIsBuildable(List<int> countryIds, Faction faction)
        {
            this.CountryIds = countryIds;
            this.Faction = faction;
        }

        public override bool MeetCondition() => 
            CountryStates.Any(cs => cs.Tags.Has(Tag.Buildable, Faction));
    }

    public class CountryIsRecruitable : Condition
    {
        public CountryIsRecruitable(List<int> countryIds, Faction faction)
        {
            this.CountryIds = countryIds;
            this.Faction = faction;
        }

        public override bool MeetCondition() => 
            CountryStates.Any(cs => cs.Tags.Has(Tag.Recruitable, Faction));
    }


    public class CountryIsAttackable : Condition
    {
        public CountryIsAttackable(List<int> countryIds, Faction faction)
        {
            this.CountryIds = countryIds;
            this.Faction = faction;
        }

        public override bool MeetCondition() => 
            CountryStates.Any(cs =>
                cs.Tags.Has(Tag.Attackable, Faction) ||
                cs.Units.Values.Any(unitId => UnitState.ForId(unitId).Tags.Has(Tag.Attackable, Faction)));
    }
    public class CountryIsEmpty : Condition
    {
        public CountryIsEmpty(int countryId)
        {
            this.CountryIds = [countryId];
        }

        public CountryIsEmpty(List<int> countryIds)
        {
            this.CountryIds = countryIds;
        }

        public override bool MeetCondition()
        {
            // Check if any of the specified countries are empty (no units)
            return CountryStates.Any(countryState => countryState.IsCountryEmpty);
        }
    }

    public class CountryHasFactionUnit : Condition
    {
        public CountryHasFactionUnit(int countryId, Faction faction)
        {
            this.CountryIds = [countryId];
            this.Faction = faction;
        }

        public override bool MeetCondition()
        {
            return CountryStates.Any(countryState => countryState.HasUnit(Faction));
        }
    }

    public class CountryHasAttackableNeighbor : Condition
    {
        public CountryHasAttackableNeighbor(int countryId)
        {
            this.CountryIds = [countryId];
        }

        public CountryHasAttackableNeighbor(List<int> countryIds)
        {
            this.CountryIds = countryIds;
        }

        public override bool MeetCondition()
        {
            return CountryStates.Any(countryState => 
                countryState.ConnectedCountryStates.Any(neighbor => neighbor.Tags.HasForAny(Tag.Attackable)));
        }
    }

    public class FactionBattled : EventCondition
    {
        public FactionBattled(Faction faction) { this.Faction = faction; }
        public FactionBattled(FactionTeam factionTeam) { this.FactionTeam = factionTeam; }

        public override bool IsMatch(ChangeEvent ce)
        {
            if (ce is not BattleCountryChangeEvent bce || !bce.IsBattle) return false;
            if (Faction != Faction.NONE && bce.TriggeringFaction != Faction) return false;
            if (this.FactionTeam != FactionTeam.NONE && StaticGameData.FactionTeamForFaction(bce.TriggeringFaction) != this.FactionTeam) return false;
            if (TargetFaction != Faction.NONE)
            {
                if (bce is not BattleUnitChangeEvent bue || bue.UnitState.Faction != TargetFaction) return false;
            }
            if (TargetFactionTeam != FactionTeam.NONE)
            {
                if (bce is not BattleUnitChangeEvent bue2 || bue2.UnitState.FactionTeam != TargetFactionTeam) return false;
            }
            if (this.CountryType != CountryType.NONE && bce.CountryState.Type != this.CountryType) return false;
            if (CountryIds?.Count > 0 && !CountryIds.Contains(bce.CountryState.Id)) return false;
            return true;
        }
    }

    public class FactionDeployed : EventCondition
    {
        public FactionDeployed(Faction faction, DeployType deployType)
        {
            this.Faction = faction;
            this.DeployType = deployType;
        }

        public override bool IsMatch(ChangeEvent ce)
        {
            if (ce is not DeployUnitChangeEvent dce) return false;
            if (dce.TriggeringFaction != Faction) return false;
            if (dce.DeploymentType != this.DeployType) return false;
            if (!this.UnitType.Matches(dce.UnitType)) return false;
            if (this.CountryType != CountryType.NONE && dce.CountryState.Type != this.CountryType) return false;
            if (CountryIds?.Count > 0 && !CountryIds.Contains(dce.CountryId)) return false;
            return true;
        }
    }   

    public class FactionHasBattleTarget : Condition
    {
        UnitType unitType;
        public FactionHasBattleTarget(Faction faction, UnitType unitType)
        {
            this.unitType = unitType;
            this.Faction = faction;
        }
        public FactionHasBattleTarget(Faction faction, UnitType unitType, List<int> countryIds)
        {
            this.unitType = unitType;
            this.Faction = faction;
            this.CountryIds = countryIds;
        }
        public override bool MeetCondition()
        {
            if (CountryIds != null && CountryIds.Count > 0)
            {
                return CountryStates.Any(countryState => countryState.Tags.Has(Tag.Attackable, Faction));
            } 
            else if (CountryIds == null || CountryIds.Count == 0)
            {
                return false;
            }
            else
            {
                // Check if any units have attackable tag for this faction
                var allUnits = GameSession.Current.GameState.UnitStatesById.Values;
                bool hasAttackableUnits = allUnits.Any(us => us.Tags.Has(Tag.Attackable, Faction) && us.Type.Matches(unitType));
                // Check if any countries have attackable tag for this faction
                bool hasAttackableCountries = CountryState.AllCountryStates.Any(cs => cs.Tags.Has(Tag.Attackable, Faction) && 
                    ((unitType.Matches(UnitType.ARMY) && cs.Type == CountryType.LAND) || (unitType.Matches(UnitType.NAVY) && cs.Type == CountryType.SEA)));
                return hasAttackableUnits || hasAttackableCountries;
            }
        }
    }

    public class FactionUnitIsAttacked : EventCondition
    {
        public FactionUnitIsAttacked(Faction faction) { this.Faction = faction; }
        public override bool IsMatch(ChangeEvent ce) => ce is BattleUnitChangeEvent bue && bue.UnitState.Faction == Faction;
    }
    public class FactionUnitInCountryIsAttacked : FactionUnitIsAttacked
    {
        public FactionUnitInCountryIsAttacked(Faction faction, List<int> countryIds) : base(faction)
        {
            this.CountryIds = countryIds;
        }
        public override bool IsMatch(ChangeEvent ce)
            => ce is BattleUnitChangeEvent bue && bue.UnitState.Faction == Faction && CountryIds.Contains(bue.CountryId);
    }
    public class FactionTeamUnitIsAttacked : EventCondition
    {
        public FactionTeamUnitIsAttacked(FactionTeam factionTeam) { this.FactionTeam = factionTeam; }
        public override bool IsMatch(ChangeEvent ce)
            => ce is BattleUnitChangeEvent bue && StaticGameData.FactionTeamForFaction(bue.UnitState.Faction) == this.FactionTeam;
    }
    public class FactionTeamUnitInCountryIsAttacked : FactionTeamUnitIsAttacked
    {
        public FactionTeamUnitInCountryIsAttacked(FactionTeam factionTeam, List<int> countryIds) : base(factionTeam)
        {
            this.CountryIds = countryIds;
        }
        public override bool IsMatch(ChangeEvent ce)
            => ce is BattleUnitChangeEvent bue
               && StaticGameData.FactionTeamForFaction(bue.UnitState.Faction) == this.FactionTeam
               && CountryIds.Contains(bue.CountryId);
    }

    /// <summary>
    /// Matches a card play by <paramref name="faction"/> anywhere in this round's change event pool.
    ///
    /// Pool-scoped deliberately (the default <see cref="ConditionScope"/>): a card keyed on this is
    /// offered in the after-reaction window of ANY step of the played card, not only the first. That
    /// breadth is the point — it is what replaced the activation window DoCard used to open on the
    /// PlayCardChangeEvent itself, which was paid for on every single card play to serve two cards.
    ///
    /// EventCondition's RequiresEventContext is always true, so a card using this passes
    /// HasEventBasedTrigger without needing CustomCondition.InReactionWindow().
    ///
    /// The pool read is sound even though DoCard never calls RegisterChangeEvent for the introduction
    /// event: every ChangeEvent.Apply() self-registers in ApplyMutation, and IsTrigger = false closes
    /// the block and after-reaction windows, not the registration.
    ///
    /// A round is one faction's one turn step, so pair this with IsFactionTurn/IsGameFlowStep on a card
    /// whose text scopes it to its owner's own Play step — EventLendLease makes another faction play a
    /// card inside the US's round, and pool scope would otherwise honour it for the rest of that round.
    /// </summary>
    public class FactionPlayedCard : EventCondition
    {
        private readonly Faction _faction;

        public FactionPlayedCard(Faction faction) { _faction = faction; }

        public override bool IsMatch(ChangeEvent ce)
            => ce is PlayCardChangeEvent playCard && playCard.TriggeringFaction == _faction;
    }

    public class IsBlockRequest : Condition
    {
        public override bool RequiresEventContext => true;

        public IsBlockRequest() {}
        public IsBlockRequest(CardType cardType) 
        {
            this.TargetCardType = cardType;
        }
        
        public override bool MeetCondition()
        {
            return this.TargetCardType != null
                ? CardPlayPool.CurrentBlockTrigger?.SourceCardState?.CardData?.CardType == this.TargetCardType
                : true;
        }
    }

    /// <summary>
    /// Block-reaction condition: the change event offered for block was produced by an ACTIVATED card
    /// of the given faction and card type.
    ///
    /// It derives from <see cref="IsBlockRequest"/> rather than sitting beside it because
    /// <see cref="CardLogic.IsBlockReaction"/> is a bare <c>is Condition.IsBlockRequest</c> test, and
    /// that is what earns Tag.IsBlockReaction and so any place in a block window at all. A sibling
    /// class would compile, read correctly, and never be offered.
    ///
    /// "Activated" needs no explicit test. A Status/Response card played from hand runs no steps at all
    /// (DoCard's isTableCardPlay guard), and an introduction event is IsTrigger = false and never
    /// reaches DoChangeEvent, so any block window whose source card is a played Status card is
    /// necessarily an activation of it.
    ///
    /// Reads CurrentBlockTrigger, never LastNoneNewCardChangeEvent — see UnitAboutToBeRemoved for why.
    /// </summary>
    public class IsBlockRequestFromCard : IsBlockRequest
    {
        private readonly Faction _faction;

        public IsBlockRequestFromCard(Faction faction, CardType cardType) : base(cardType) { _faction = faction; }

        public override bool MeetCondition()
            => base.MeetCondition() && CardPlayPool.CurrentBlockTrigger?.TriggeringFaction == _faction;
    }

    /// <summary>
    /// Block-reaction condition: the change event being offered for block is a RemoveUnitChangeEvent
    /// matching optional faction, unit type, supply, and country filters.
    ///
    /// Matches only while a block window is open. It deliberately reads CurrentBlockTrigger rather
    /// than LastNoneNewCardChangeEvent: that property skips PlayCardChangeEvent and
    /// ActivateReactionChangeEvent, so inside a card play's or reaction activation's block window it
    /// reports the earlier removal and every removal-blocker matched against a card being played.
    /// </summary>
    public class UnitAboutToBeRemoved : Condition
    {
        public override bool RequiresEventContext => true;
        private readonly bool _requireInSupply;

        /// <summary>Matches any RemoveUnitChangeEvent, optionally requiring in-supply.</summary>
        public UnitAboutToBeRemoved(bool requireInSupply = false)
        {
            _requireInSupply = requireInSupply;
        }

        /// <summary>Matches a RemoveUnitChangeEvent for a specific faction and unit type.</summary>
        public UnitAboutToBeRemoved(Faction targetFaction, UnitType unitType = UnitType.ANY, bool requireInSupply = false)
        {
            this.TargetFaction = targetFaction;
            this.UnitType = unitType;
            _requireInSupply = requireInSupply;
        }

        /// <summary>Matches a RemoveUnitChangeEvent for any of the given factions and a specific unit type.</summary>
        public UnitAboutToBeRemoved(List<Faction> targetFactions, UnitType unitType = UnitType.ANY, bool requireInSupply = false)
        {
            TargetFactions = targetFactions;
            this.UnitType = unitType;
            _requireInSupply = requireInSupply;
        }

        public override bool MeetCondition()
        {
            if (CardPlayPool.CurrentBlockTrigger is not RemoveUnitChangeEvent removeEvent)
                return false;
            if (TargetFactions?.Count > 0 && !TargetFactions.Contains(removeEvent.UnitState.Faction))
                return false;
            if ((TargetFactions == null || TargetFactions.Count == 0)
                && TargetFaction != Faction.NONE && removeEvent.UnitState.Faction != TargetFaction)
                return false;
            if (TargetFactionTeam != FactionTeam.NONE
                && StaticGameData.FactionTeamForFaction(removeEvent.UnitState.Faction) != TargetFactionTeam)
                return false;
            if (!UnitType.Matches(removeEvent.UnitState.Type))
                return false;
            if (_requireInSupply && !removeEvent.UnitState.InSupply)
                return false;
            if (CountryIds?.Count > 0 && !CountryIds.Contains(removeEvent.CountryId))
                return false;
            return true;
        }
    }

    /// <summary>
    /// After-reaction or block-reaction condition: a DeployUnitChangeEvent is present in the pool
    /// matching optional faction, faction team, unit type, and country filters.
    /// </summary>
    public class UnitAboutToBeDeployed : Condition
    {
        public override bool RequiresEventContext => true;
        /// <summary>Matches any DeployUnitChangeEvent.</summary>
        public UnitAboutToBeDeployed() { }

        /// <summary>Matches a DeployUnitChangeEvent from a specific faction and unit type.</summary>
        public UnitAboutToBeDeployed(Faction targetFaction, UnitType unitType = UnitType.ANY)
        {
            this.TargetFaction = targetFaction;
            this.UnitType = unitType;
        }

        /// <summary>Matches a DeployUnitChangeEvent from any of the given factions and a specific unit type.</summary>
        public UnitAboutToBeDeployed(List<Faction> targetFactions, UnitType unitType = UnitType.ANY)
        {
            TargetFactions = targetFactions;
            this.UnitType = unitType;
        }

        /// <summary>Matches a DeployUnitChangeEvent from a faction team and optional unit type.</summary>
        public UnitAboutToBeDeployed(FactionTeam targetFactionTeam, UnitType unitType = UnitType.ANY)
        {
            this.TargetFactionTeam = targetFactionTeam;
            this.UnitType = unitType;
        }

        public override bool MeetCondition()
        {
            return CardPlayPool.GetChangeEvents<DeployUnitChangeEvent>().Any(ce =>
            {
                if (TargetFactions?.Count > 0 && !TargetFactions.Contains(ce.TriggeringFaction))
                    return false;
                if ((TargetFactions == null || TargetFactions.Count == 0)
                    && TargetFaction != Faction.NONE && ce.TriggeringFaction != TargetFaction)
                    return false;
                if (TargetFactionTeam != FactionTeam.NONE
                    && StaticGameData.FactionTeamForFaction(ce.TriggeringFaction) != TargetFactionTeam)
                    return false;
                if (!UnitType.Matches(ce.UnitType))
                    return false;
                if (CountryIds?.Count > 0 && !CountryIds.Contains(ce.CountryId))
                    return false;
                return true;
            });
        }
    }

    public class CardInPlay : Condition
    {
        public CardInPlay(int cardId) { this.CardIds = [cardId]; }
        public CardInPlay(CardState cardState) { this.CardIds = [cardState.Id]; }

        public override bool MeetCondition()
        {
            if (CardState.CardData.CardType != CardType.STATUS && CardState.CardData.CardType != CardType.RESPONSE)
            {
                throw new Exception("Card is not status or response");
            }
            return CardState.IsPlayed;
        }
    }

    public class IsGameFlowStep : Condition
    {
        public IsGameFlowStep(TurnStep turnStep)
        {
            this.TurnStep = turnStep;
        }

        public override bool MeetCondition()
        {
            return GameFlow.Instance.TurnStep == this.TurnStep;
        }
    }

    // Helper conditions for common board state queries
    public class HasBuildableLand : Condition
    {
        public HasBuildableLand(Faction faction) { this.Faction = faction; }
        public override bool MeetCondition() => CountryState.BuildableLand(Faction).Any();
    }

    public class HasBuildableSea : Condition
    {
        public HasBuildableSea(Faction faction) { this.Faction = faction; }
        public override bool MeetCondition() => CountryState.BuildableSea(Faction).Any();
    }

    /// <summary>
    /// A buildable land space the faction does NOT already occupy — a build that would actually change
    /// the board, as opposed to the rebuild-in-place that <see cref="CountryState.CanBuild"/> also
    /// permits (the piece standing there is redeployed, so nothing moves). Intended as a
    /// <see cref="CardStep.WithAdvisoryCondition"/>, not as a gate: rebuilding in place is legal.
    /// </summary>
    public class HasVacantBuildableLand : Condition
    {
        public HasVacantBuildableLand(Faction faction) { this.Faction = faction; }
        public override bool MeetCondition() => CountryState.BuildableLand(Faction).Any(cs => !cs.HasUnit(Faction));
    }

    /// <inheritdoc cref="HasVacantBuildableLand"/>
    public class HasVacantBuildableSea : Condition
    {
        public HasVacantBuildableSea(Faction faction) { this.Faction = faction; }
        public override bool MeetCondition() => CountryState.BuildableSea(Faction).Any(cs => !cs.HasUnit(Faction));
    }

    /// <summary>
    /// The faction still has a piece of this type in its pool. Intended as a
    /// <see cref="CardStep.WithAdvisoryCondition"/>, not as a gate: deploying at zero pool is legal —
    /// <see cref="UnitPoolShortfall.ResolveBeforeDeploy"/> asks the player to recall one of their own
    /// units to fund it — it just costs a piece somewhere else, which is rarely what a player reaching
    /// for a build card meant. Gating on it would make build cards silently unplayable at zero pool,
    /// which <see cref="CountryState.CanBuild"/> deliberately avoids.
    ///
    /// Pass a real bucket, never <see cref="UnitType.ANY"/>: that is a filter wildcard, and
    /// UnitPool.AvailableUnitCount counts nothing for it.
    /// </summary>
    public class HasAvailableUnits : Condition
    {
        public HasAvailableUnits(Faction faction, UnitType unitType)
        {
            this.Faction = faction;
            this.UnitType = unitType;
        }
        public override bool MeetCondition() => UnitPool.FactionHasAvailableUnits(Faction, UnitType);
    }

    public class HasRecruitableLand : Condition
    {
        public HasRecruitableLand(Faction faction) { this.Faction = faction; }
        public override bool MeetCondition() => CountryState.RecruitableLand(Faction).Any();
    }

    public class HasRecruitableSea : Condition
    {
        public HasRecruitableSea(Faction faction) { this.Faction = faction; }
        public override bool MeetCondition() => CountryState.RecruitableSea(Faction).Any();
    }

    public class HasAttackableLand : Condition
    {
        public HasAttackableLand(Faction faction) { this.Faction = faction; }
        public override bool MeetCondition() => CountryState.AttackableLand(Faction).Any();
    }

    public class HasAttackableSea : Condition
    {
        public HasAttackableSea(Faction faction) { this.Faction = faction; }
        public override bool MeetCondition() => CountryState.AttackableSea(Faction).Any();
    }

    public class HasLandBattleTarget : Condition
    {
        public HasLandBattleTarget(Faction faction) { this.Faction = faction; }
        public override bool MeetCondition() => 
            UnitState.AttackableArmies(Faction).Any() || CountryState.AttackableLand(Faction).Any();
    }

    public class HasSeaBattleTarget : Condition
    {
        public HasSeaBattleTarget(Faction faction) { this.Faction = faction; }
        public override bool MeetCondition() => 
            UnitState.AttackableNavies(Faction).Any() || CountryState.AttackableSea(Faction).Any();
    }

    public class HasDeployedArmy : EventCondition
    {
        public HasDeployedArmy(Faction faction) { this.Faction = faction; }
        public override bool IsMatch(ChangeEvent ce)
            => ce is DeployUnitChangeEvent dce && dce.TriggeringFaction == Faction && dce.DeploymentType == DeployType.BUILD && dce.UnitType == UnitType.ARMY;
    }

    public class HasDeployedNavy : EventCondition
    {
        public HasDeployedNavy(Faction faction) { this.Faction = faction; }
        public HasDeployedNavy(FactionTeam factionTeam) { this.FactionTeam = factionTeam; }
        public override bool IsMatch(ChangeEvent ce)
        {
            if (ce is not DeployUnitChangeEvent dce) return false;
            if (dce.DeploymentType != DeployType.BUILD || dce.UnitType != UnitType.NAVY) return false;
            if (Faction != Faction.NONE && dce.TriggeringFaction != Faction) return false;
            if (FactionTeam != FactionTeam.NONE && StaticGameData.FactionTeamForFaction(dce.TriggeringFaction) != FactionTeam) return false;
            return true;
        }
    }

    public class HasBattledOnLand : EventCondition
    {
        public HasBattledOnLand(Faction faction) { this.Faction = faction; }
        public override bool IsMatch(ChangeEvent ce)
            => ce is BattleCountryChangeEvent bce && bce.IsBattle && bce.TriggeringFaction == Faction && bce.CountryState.Type == CountryType.LAND;
    }

    public class HasBattledAtSea : EventCondition
    {
        public HasBattledAtSea(Faction faction) { this.Faction = faction; }
        public override bool IsMatch(ChangeEvent ce)
            => ce is BattleCountryChangeEvent bce && bce.IsBattle && bce.TriggeringFaction == Faction && bce.CountryState.Type == CountryType.SEA;
    }

    public class HasPlayedCardThisTurnStep : Condition
    {
        public HasPlayedCardThisTurnStep(Faction faction) { this.Faction = faction; }
        public override bool MeetCondition() => For(Faction);

        /// <summary>
        /// Whether the faction has already spent its play this turn step. Both a hand play
        /// (PlayCardChangeEvent) and a play-step activation (SpendPlayActionChangeEvent) increment the
        /// counter, so this is the single "the play is gone" test. Static so
        /// <see cref="IsPlayCardStep"/> can fold it in without allocating a condition per check.
        /// </summary>
        public static bool For(Faction faction)
            => GameFlow.Instance.CardsPlayedThisTurnStep.ContainsKey(faction)
               && GameFlow.Instance.CardsPlayedThisTurnStep[faction] > 0;
    }

    public class IsFactionTurn : Condition
    {
        public IsFactionTurn(Faction faction) { this.Faction = faction; }
        public override bool MeetCondition() => GameFlow.Instance.CurrentFaction == Faction;
    }

    public class IsVictoryPointStep : Condition
    {
        public override bool MeetCondition() => GameFlow.Instance.TurnStep == TurnStep.VICTORY_POINT;
    }

    /// <summary>
    /// The Play step, with the faction's play still unspent — the window in which a table card can be
    /// activated instead of, or ahead of, playing a card from hand.
    ///
    /// Not every card carrying this spends the play. Most do (Conscription, Bravado, Guards, … each
    /// emit SpendPlayActionChangeEvent as their first step, which is the literal "instead of playing
    /// a card from hand"). The four that read "at the beginning of your turn" — Volksturm, Superior
    /// Planning, Mobile Force, Defense of the Motherland — carry it purely for its TIMING and leave
    /// the play intact; CardPlayRound.Start keeps re-asking until the play is actually spent. They
    /// used to live in a TurnStep.START window of their own, which announced that the faction held a
    /// start-step card — for a face-down Response, exactly the card it was hiding.
    ///
    /// The "not yet played" half is folded in rather than left to each card to add its own
    /// Not(HasPlayedCardThisTurnStep): every card carrying this condition needs it (the play-spending
    /// ones would otherwise grant a free extra action; the free ones would otherwise be usable after
    /// the hand card, which is not "the beginning of your turn"), and a new card that forgot it would
    /// silently be activatable twice in a step.
    /// The six cards that predate this keep their explicit Not(...) — it is now redundant, not wrong.
    ///
    /// This condition is also the marker for "belongs beside the hand in the play prompt" — see
    /// <see cref="CardLogic.IsPlayStepActivation"/>, which tests for its PRESENCE, not whether it is
    /// met, so a card whose play is already spent is still shown (greyed out) rather than vanishing.
    /// </summary>
    public class IsPlayCardStep : Condition
    {
        public override bool MeetCondition()
        {
            if (GameFlow.Instance.TurnStep != TurnStep.PLAY_CARD)
                return false;

            // CardLogic is set by Condition.Build, which every card trigger goes through. A bare
            // instance has no owner to attribute the spent play to, so it keeps the plain step check.
            Faction faction = CardLogic?.Faction ?? Faction.NONE;
            if (faction == Faction.NONE)
                return true;

            return !HasPlayedCardThisTurnStep.For(faction);
        }
    }

    // An IsStartStep condition used to live here, for the four cards that read "at the beginning of
    // your turn". It is gone with the TurnStep.START card round: those cards carry IsPlayCardStep
    // now, so their window is the play prompt that opens every turn regardless of what anyone holds.
    // Re-adding it would re-open the leak — a window that exists only when a card can use it is proof
    // that the card is there.

    public class CustomCondition : Condition
    {
        Func<bool> Condition;
        private bool _requiresEventContext;

        public CustomCondition(Func<bool> condition) => this.Condition = condition;

        /// <summary>
        /// Marks this predicate as event-scoped: it inspects <c>CardPlayPool.CurrentReactionTrigger</c>
        /// or the change-event pool, so the card may activate inside a reaction chain.
        /// Without it, <see cref="CardLogic.CanBeActivated"/> rejects the card whenever ReactionDepth &gt; 0.
        /// </summary>
        public CustomCondition InReactionWindow() { _requiresEventContext = true; return this; }

        public override bool RequiresEventContext => _requiresEventContext;
        public override bool MeetCondition() => Condition.Invoke();
    }

    /// <summary>
    /// Base class for event-based conditions. Subclasses override <see cref="IsMatch"/> to check a single event.
    /// <para>
    /// <b>Pool</b> (default): scans the full event pool — "did this happen this round?"<br/>
    /// Use for status card scoring and step conditions.<br/><br/>
    /// <b>Immediate</b> (via <see cref="Immediately"/>): checks only <c>CardPlayPool.CurrentReactionTrigger</c> — "is the current reaction window triggered by this event?"<br/>
    /// Use in response card <c>CardTriggers()</c> for "immediately after X" reactions.
    /// </para>
    /// </summary>
    public abstract class EventCondition : Condition
    {
        public override bool RequiresEventContext => true;
        private ConditionScope _scope = ConditionScope.Pool;

        /// <summary>Scopes this condition to the current reaction window trigger instead of the full pool. Use in response card <c>CardTriggers()</c>.</summary>
        public Condition Immediately() { _scope = ConditionScope.Immediate; return this; }

        /// <summary>True when this condition has been scoped with <see cref="Immediately"/>.</summary>
        public bool IsImmediate => _scope == ConditionScope.Immediate;

        public virtual bool IsMatch(ChangeEvent ce) => false;

        public override bool MeetCondition() =>
            _scope == ConditionScope.Immediate
                ? IsMatch(CardPlayPool.CurrentReactionTrigger)
                : CardPlayPool.ChangeEventsPool.Any(IsMatch);
    }

    public Condition WithCountries(List<int> countryIds)
    {
        this.CountryIds = countryIds;
        return this;
    }
    public Condition WithCountries(List<Country> countries)
    {
        this.CountryIds = countries.Map(c => (int)c);
        return this;
    }
    public Condition WithCountries(List<CountryState> countryStates)
    {
        this.CountryIds = countryStates.Map(countryStates => countryStates.Id);
        return this;
    }
    public Condition WithUnits(List<int> unitIds)
    {
        this.UnitIds = unitIds;
        return this;
    }
    public Condition WithUnits(List<Country> units)
    {
        this.UnitIds = units.Map(c => (int)c);
        return this;
    }
    public Condition WithUnits(List<CountryState> unitStates)
    {
        this.UnitIds = unitStates.Map(countryStates => countryStates.Id);
        return this;
    }
    public Condition WithUnitType(UnitType unitType)
    {
        this.UnitType = unitType;
        return this;
    }
    public Condition WithCountryType(CountryType countryType)
    {
        this.CountryType = countryType;
        return this;
    }
    public Condition WithTargetFaction(Faction faction)
    {
        this.TargetFaction = faction;
        return this;
    }
    public Condition WithTargetFactionTeam(FactionTeam factionTeam)
    {
        this.TargetFactionTeam = factionTeam;
        return this;
    }

    private void filterCountryStates(ref List<CountryState> countryStates)
    {
        if (CountryType != CountryType.NONE)
        {
            countryStates = countryStates.Where(countryState => countryState.Type == CountryType).ToList();
        }
        if (CountryIds?.Count > 0)
        {
            countryStates = countryStates.Where(countryState => CountryIds.Contains(countryState.Id)).ToList();
        }
    }
}
