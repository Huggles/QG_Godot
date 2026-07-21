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
    public class CountryHasEnemyUnit : Condition
    {
        public CountryHasEnemyUnit(int countryId, Faction faction)
        {
            this.CountryIds = [countryId];
            this.Faction = faction;
        }

        public CountryHasEnemyUnit(List<int> countryIds, Faction faction)
        {
            this.CountryIds = countryIds;
            this.Faction = faction;
        }

        public override bool MeetCondition()
        {
            return CountryStates.Any(countryState => countryState.Tags.Has(Tag.EnemyControlled, Faction));
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
            if (ce is not BattleCountryChangeEvent bce) return false;
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
    /// Matches an <see cref="ActivateReactionChangeEvent"/> from an optional faction and/or card type.
    /// Use with <see cref="EventCondition.Immediately"/> in <c>CardTriggers()</c> to react to a
    /// card being activated (fires in the activation window opened by DoCard, immediately after
    /// the card is activated and block reactions resolve, before its own steps execute).
    /// </summary>
    public class CardActivated : EventCondition
    {
        private readonly Faction _faction;
        private readonly CardType? _cardType;

        public CardActivated(Faction faction = Faction.NONE, CardType? cardType = null)
        {
            _faction = faction;
            _cardType = cardType;
        }

        public override bool IsMatch(ChangeEvent ce)
        {
            if (ce is not ActivateReactionChangeEvent) return false;
            if (_faction != Faction.NONE && ce.TriggeringFaction != _faction) return false;
            if (_cardType.HasValue && ce.SourceCardState?.CardData.CardType != _cardType.Value) return false;
            return true;
        }
    }

    public class IsBlockRequest : Condition
    {
        public override bool RequiresEventContext => true;
        public override bool MeetCondition()
        {
            return true;
        }
    }

    /// <summary>
    /// Block-reaction condition: the pending change event is a RemoveUnitChangeEvent
    /// matching optional faction, unit type, supply, and country filters.
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
            if (CardPlayPool.LastNoneNewCardChangeEvent is not RemoveUnitChangeEvent removeEvent)
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
            => ce is BattleCountryChangeEvent bce && bce.TriggeringFaction == Faction && bce.CountryState.Type == CountryType.LAND;
    }

    public class HasBattledAtSea : EventCondition
    {
        public HasBattledAtSea(Faction faction) { this.Faction = faction; }
        public override bool IsMatch(ChangeEvent ce)
            => ce is BattleCountryChangeEvent bce && bce.TriggeringFaction == Faction && bce.CountryState.Type == CountryType.SEA;
    }

    public class HasPlayedCardThisTurnStep : Condition
    {
        public HasPlayedCardThisTurnStep(Faction faction) { this.Faction = faction; }
        public override bool MeetCondition() => GameFlow.Instance.CardsPlayedThisTurnStep.ContainsKey(Faction) && GameFlow.Instance.CardsPlayedThisTurnStep[Faction] > 0;
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

    public class IsPlayCardStep : Condition
    {
        public override bool MeetCondition() => GameFlow.Instance.TurnStep == TurnStep.PLAY_CARD;
    }

    public class IsStartStep : Condition
    {
        public override bool MeetCondition() => GameFlow.Instance.TurnStep == TurnStep.START;
    }

    public class CustomCondition : Condition
    {
        Func<bool> Condition;
        public CustomCondition(Func<bool> condition) => this.Condition = condition;
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
