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
    TurnStep TurnStep;
    DeployType DeployType;

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


        public override bool MeetCondition()
        {
            return CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>().Any(changeEvent => changeEvent.TriggeringFaction == Faction);
        }
    }
    public class FactionBattledCountry : FactionBattled
    {
        public FactionBattledCountry(Faction faction, List<int> countryIds) : base(faction)
        {
            this.CountryIds = countryIds;
        }

        public override bool MeetCondition()
        {
            bool MeetCondition = CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>().Any(changeEvent => changeEvent.TriggeringFaction == Faction && CountryIds.Contains(changeEvent.CountryId));
            DebugUtilities.PrintPeer($"{Faction} battled in countries {MeetCondition}: {String.Join(",", CountryStates.Map(cs => cs.Name))}");
            return MeetCondition;
        }
    }
    public class FactionTeamBattled : Condition
    {
        public FactionTeamBattled(FactionTeam factionTeam)
        {
            this.FactionTeam = factionTeam;
        }
        public override bool MeetCondition()
        {
            return CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>().Any(changeEvent => StaticGameData.FactionTeamForFaction(changeEvent.TriggeringFaction) == this.FactionTeam);
        }
    }
    public class FactionTeamBattledCountry : FactionTeamBattled
    {
        public FactionTeamBattledCountry(FactionTeam factionTeam, List<int> countryIds) : base(factionTeam)
        {
            this.CountryIds = countryIds;
        }
        public override bool MeetCondition()
        {
            return CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>().Any(changeEvent => StaticGameData.FactionTeamForFaction(changeEvent.TriggeringFaction) == this.FactionTeam);
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
            if (this.DeployType == DeployType.ANY)
            {
                return CardPlayPool.GetChangeEvents<DeployUnitChangeEvent>().Any(changeEvent => changeEvent.TriggeringFaction == Faction);
            }
            else
            {
                return CardPlayPool.GetChangeEvents<DeployUnitChangeEvent>().Any(changeEvent => changeEvent.TriggeringFaction == Faction && changeEvent.DeploymentType == this.DeployType);
            }

        }
    }
    public class FactionDeployedCountry : FactionDeployed
    {
        public FactionDeployedCountry(Faction faction, DeployType deployType, List<int> countryIds) : base(faction, deployType)
        {
            this.CountryIds = countryIds;
        }

        public override bool MeetCondition()
        {
            if (this.DeployType == DeployType.ANY)
            {
                return CardPlayPool.GetChangeEvents<DeployUnitChangeEvent>().Any(changeEvent => changeEvent.TriggeringFaction == Faction && CountryIds.Contains(changeEvent.CountryId));
            }
            else
            {
                return CardPlayPool.GetChangeEvents<DeployUnitChangeEvent>().Any(
                    changeEvent => changeEvent.TriggeringFaction == Faction &&
                    CountryIds.Contains(changeEvent.CountryId) &&
                    changeEvent.DeploymentType == this.DeployType);
            }
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
                AttackState attackState = AttackState.AttackStateForFaction(Faction);
                return attackState.TargetsOfType(unitType).Count > 0;
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
}
