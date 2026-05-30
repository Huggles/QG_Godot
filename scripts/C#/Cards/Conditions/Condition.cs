using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public abstract class Condition
{
    public abstract bool MeetCondition();
    public CardLogic CardLogic;

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
                countryState.NeighborCountryStates.Any(neighbor => neighbor.Tags.HasForAny(Tag.Attackable)));
        }
    }

    public class FactionBattled : Condition
    {
        public FactionBattled(Faction faction)
        {
            this.Faction = faction;
        }
        public FactionBattled(FactionTeam factionTeam)
        {
            this.FactionTeam = factionTeam;
        }
        public override bool MeetCondition()
        {
            List<BattleCountryChangeEvent> battleCountryChangeEvents = CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>();
            if (Faction != Faction.NONE)
            {
                battleCountryChangeEvents = battleCountryChangeEvents.Where(changeEvent => changeEvent.TriggeringFaction == Faction).ToList();
            }
            else
            {
                battleCountryChangeEvents = battleCountryChangeEvents.Where(changeEvent => StaticGameData.FactionTeamForFaction(changeEvent.TriggeringFaction) == this.FactionTeam).ToList();
            }

            if (TargetFaction != Faction.NONE)
            {
                battleCountryChangeEvents = battleCountryChangeEvents.Where(changeEvent => changeEvent is BattleUnitChangeEvent battleUnitChangeEvent && battleUnitChangeEvent.UnitState.Faction == TargetFaction).ToList();
            }
            if (TargetFactionTeam != FactionTeam.NONE)
            {
                battleCountryChangeEvents = battleCountryChangeEvents.Where(changeEvent => changeEvent is BattleUnitChangeEvent battleUnitChangeEvent && battleUnitChangeEvent.UnitState.FactionTeam == TargetFactionTeam).ToList();
            }

            List<CountryState> countryStates = battleCountryChangeEvents.Map(ce => ce.CountryState).ToList();
            filterCountryStates(ref countryStates);           
            return countryStates.Count > 0;
        }
    }

    public class FactionDeployed : Condition
    {
        public FactionDeployed(Faction faction, DeployType deployType)
        {
            this.Faction = faction;
            this.DeployType = deployType;
        }

        public override bool MeetCondition()
        {
            List<DeployUnitChangeEvent> deployUnitChangeEvents = CardPlayPool.GetChangeEvents<DeployUnitChangeEvent>();
            deployUnitChangeEvents = deployUnitChangeEvents.Where(ce => ce.TriggeringFaction == this.Faction).ToList();
            deployUnitChangeEvents = deployUnitChangeEvents.Where(ce => ce.DeploymentType == this.DeployType).ToList();
            if (this.UnitType != UnitType.ANY)
            {
                deployUnitChangeEvents = deployUnitChangeEvents.Where(ce => ce.UnitType == this.UnitType).ToList();
            }
            List<CountryState> countryStates = deployUnitChangeEvents.Map(ce => ce.CountryState).ToList();
            filterCountryStates(ref countryStates);
            
            return countryStates.Count > 0;
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
            else
            {
                // Check if any units have attackable tag for this faction
                var allUnits = GameSession.Current.GameState.UnitStatesById.Values;
                bool hasAttackableUnits = allUnits.Any(us => us.Tags.Has(Tag.Attackable, Faction) && (unitType == UnitType.ANY || us.Type == unitType));
                // Check if any countries have attackable tag for this faction
                bool hasAttackableCountries = CountryState.AllCountryStates.Any(cs => cs.Tags.Has(Tag.Attackable, Faction) && 
                    (unitType == UnitType.ANY || (unitType == UnitType.ARMY && cs.Type == CountryType.LAND) || (unitType == UnitType.NAVY && cs.Type == CountryType.SEA)));
                return hasAttackableUnits || hasAttackableCountries;
            }
        }
    }

    public class FactionUnitIsAttacked : Condition
    {
        public FactionUnitIsAttacked(Faction faction)
        {
            this.Faction = faction;
        }
        public override bool MeetCondition()
        {
            return CardPlayPool.GetChangeEvents<BattleUnitChangeEvent>().Any(changeEvent => changeEvent.UnitState.Faction == Faction);
        }
    }
    public class FactionUnitInCountryIsAttacked : FactionUnitIsAttacked
    {
        public FactionUnitInCountryIsAttacked(Faction faction, List<int> countryIds) : base(faction)
        {
            this.CountryIds = countryIds;
        }
        public override bool MeetCondition()
        {
            return CardPlayPool.GetChangeEvents<BattleUnitChangeEvent>().Any(changeEvent => changeEvent.UnitState.Faction == Faction && this.CountryIds.Contains(changeEvent.CountryId));
        }
    }
    public class FactionTeamUnitIsAttacked : Condition
    {
        public FactionTeamUnitIsAttacked(FactionTeam factionTeam)
        {
            this.FactionTeam = factionTeam;
        }
        public override bool MeetCondition()
        {
            return CardPlayPool.GetChangeEvents<BattleUnitChangeEvent>().Any(changeEvent => StaticGameData.FactionTeamForFaction(changeEvent.UnitState.Faction) == this.FactionTeam);
        }
    }
    public class FactionTeamUnitInCountryIsAttacked : FactionTeamUnitIsAttacked
    {
        public FactionTeamUnitInCountryIsAttacked(FactionTeam factionTeam, List<int> countryIds) : base(factionTeam)
        {
            this.CountryIds = countryIds;
        }
        public override bool MeetCondition()
        {
            return CardPlayPool.GetChangeEvents<BattleUnitChangeEvent>().Any(changeEvent => StaticGameData.FactionTeamForFaction(changeEvent.UnitState.Faction) == this.FactionTeam);
        }
    }

    public class IsBlockRequest : Condition
    {
        public override bool MeetCondition()
        {
            return true;
        }
    }

    public class CardInPlay : Condition
    {
        public CardInPlay(int cardId) { this.CardIds = [cardId]; }
        public CardInPlay(CardState cardState) { this.CardIds = [cardState.Id]; }

        public override bool MeetCondition()
        {
            if (CardState.CardData.CardType != CardType.STATUS || CardState.CardData.CardType != CardType.RESPONSE)
            {
                throw new Exception("Card is not status or response");
            }
            return CardState.CardLogic.IsPlayed;
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
            return GameSession.Current.GameFlow.TurnStep == this.TurnStep;
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

    public class HasDeployedArmy : Condition
    {
        public HasDeployedArmy(Faction faction) { this.Faction = faction; }
        public override bool MeetCondition()
        {
            return CardPlayPool.GetChangeEvents<DeployUnitChangeEvent>()
                .Any(ce => ce.TriggeringFaction == Faction && 
                           ce.DeploymentType == DeployType.BUILD && 
                           ce.UnitType == UnitType.ARMY);
        }
    }

    public class HasDeployedNavy : Condition
    {
        public HasDeployedNavy(Faction faction) { this.Faction = faction; }
        public override bool MeetCondition()
        {
            return CardPlayPool.GetChangeEvents<DeployUnitChangeEvent>()
                .Any(ce => ce.TriggeringFaction == Faction && 
                           ce.DeploymentType == DeployType.BUILD && 
                           ce.UnitType == UnitType.NAVY);
        }
    }

    public class HasBattledOnLand : Condition
    {
        public HasBattledOnLand(Faction faction) { this.Faction = faction; }
        public override bool MeetCondition()
        {
            var battleEvents = CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>()
                .Where(ce => ce.TriggeringFaction == Faction).ToList();
            var countryStates = battleEvents.Map(ce => ce.CountryState).ToList();
            return countryStates.Any(cs => cs.Type == CountryType.LAND);
        }
    }

    public class HasBattledAtSea : Condition
    {
        public HasBattledAtSea(Faction faction) { this.Faction = faction; }
        public override bool MeetCondition()
        {
            var battleEvents = CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>()
                .Where(ce => ce.TriggeringFaction == Faction).ToList();
            var countryStates = battleEvents.Map(ce => ce.CountryState).ToList();
            return countryStates.Any(cs => cs.Type == CountryType.SEA);
        }
    }

    public class IsVictoryPointStep : Condition
    {
        public override bool MeetCondition() => 
            GameSession.Current.GameFlow.TurnStep == TurnStep.VICTORY_POINT;
    }

    public class IsPlayCardStep : Condition
    {
        public override bool MeetCondition() => 
            GameSession.Current.GameFlow.TurnStep == TurnStep.PLAY_CARD;
    }

    public class IsStartStep : Condition
    {
        public override bool MeetCondition() => 
            GameSession.Current.GameFlow.TurnStep == TurnStep.START;
    }

    public class CustomCondition : Condition
    {
        Func<bool> Condition;
        public CustomCondition(Func<bool> condition)
        {
            this.Condition = condition;
        }

        public override bool MeetCondition()
        {
            return Condition.Invoke();
        }
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
