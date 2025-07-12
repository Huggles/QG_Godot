using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class LandBattle : CardLogic
{    
    public override List<CardStep> InitializePlayCardSteps()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                List<int> armyUnits = attackState.TargetUnitIds.Where(unitId => UnitState.ForId(unitId).Type == UnitType.ARMY).ToList();
                List<int> emptyCountries = attackState.TargetEmptyCountryIds.Where(countryId => CountryState.ForId(countryId).Type == CountryType.LAND).ToList();
                BattleTarget target = await new SelectBattleTargetHandler(emptyCountries, armyUnits).Handle();
                BattleCountryChangeEvent battleCountryChange = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                battleCountryChange.IsTrigger = true;
                _ = CardPlayPool.DoChangeEvent(battleCountryChange);
            }).WithCondition(()=> Condition.Build(new Condition.FactionHasBattleTarget(Faction, UnitType.ARMY),this))
        }; 
    }

    public AttackState attackState
    {
        get
        {
            return AttackState.AttackStateForFaction(Faction);
        }
    }
}
