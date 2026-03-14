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
                GameStateCalculator calculator = GameStateCalculator.CalculateAllForFaction(Faction);
                List<int> armyUnits = calculator.TargetUnitIds.Where(unitId => UnitState.ForId(unitId).Type == UnitType.ARMY).ToList();
                List<int> emptyCountries = calculator.TargetEmptyCountryIds.Where(countryId => CountryState.ForId(countryId).Type == CountryType.LAND).ToList();
                BattleTarget target = await new SelectBattleTargetHandler(emptyCountries, armyUnits).Handle();
                BattleCountryChangeEvent battleCountryChange = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                battleCountryChange.IsTrigger = true;
                return battleCountryChange;
            }).WithCondition(()=> Condition.Build(new Condition.HasLandBattleTarget(Faction), this))
        }; 
    }
}
