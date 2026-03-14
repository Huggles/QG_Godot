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
        public CountryIsBuildable(int countryId, Faction faction)
        {
            this.CountryIds = [countryId];
            this.Faction = faction;
        }

        public CountryIsBuildable(List<int> countryIds, Faction faction)
        {
            this.CountryIds = countryIds;
            this.Faction = faction;
        }
        public CountryIsBuildable(Country country, Faction faction)
        {
            this.CountryIds = [(int)country];
            this.Faction = faction;
        }
        public CountryIsBuildable(List<Country> countries, Faction faction)
        {
            this.CountryIds = countries.Map(country => (int)country);
            this.Faction = faction;
        }


        public override bool MeetCondition()
        {
            return CountryStates.Any(countryState => countryState.CanBuild(Faction));
        }
    }

    public class CountryIsRecruitable : Condition
    {
        public CountryIsRecruitable(int countryId, Faction faction)
        {
            this.CountryIds = [countryId];
            this.Faction = faction;
        }

        public CountryIsRecruitable(List<int> countryIds, Faction faction)
        {
            this.CountryIds = countryIds;
            this.Faction = faction;
        }
        public CountryIsRecruitable(Country country, Faction faction)
        {
            this.CountryIds = [(int)country];
            this.Faction = faction;
        }
        public CountryIsRecruitable(List<Country> countries, Faction faction)
        {
            this.CountryIds = countries.Map(country => (int)country);
            this.Faction = faction;
        }

        public override bool MeetCondition()
        {
            return CountryStates.Any(countryState => countryState.CanRecruit(Faction));
        }
    }


    public class CountryIsAttackable : Condition
    {
        public CountryIsAttackable(int countryId, Faction faction)
        {
            this.CountryIds = [countryId];
            this.Faction = faction;
        }

        public CountryIsAttackable(List<int> countryIds, Faction faction)
        {
            this.CountryIds = countryIds;
            this.Faction = faction;
        }

        public override bool MeetCondition()
        {
            return CountryStates.Any(countryState => countryState.CanAttack(Faction) || countryState.CanAttackWhenEmpty(Faction));
        }
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
            return CountryStates.Any(countryState => countryState.Units.Count == 0);
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
            return CountryStates.Any(countryState => countryState.OccupyingTeam == StaticGameData.OpponentFactionTeamForFaction(Faction));
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
            return CountryStates.Any(countryState => countryState.Units.Count == 0);
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
                CountryType targetCountryType = this.unitType == UnitType.ARMY ? CountryType.LAND : CountryType.SEA;
                return CountryStates.Any(countryState => countryState.AdjacentBattleTargets(Faction, targetCountryType).Count > 0);
            }
            else
            {
                GameStateCalculator calculator = GameStateCalculator.GetCachedForFaction(Faction);
                return calculator.TargetsOfType(unitType).Count > 0;
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
            return GameSession.Instance.GameFlow.TurnStep == this.TurnStep;
        }
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
