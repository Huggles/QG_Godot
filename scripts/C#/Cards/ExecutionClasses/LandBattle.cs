using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class LandBattle : CardLogic
{    
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                List<int> armyUnits = UnitState.AttackableArmyIds(Faction);
                List<int> emptyCountries = CountryState.AttackableLandIds(Faction);
                BattleTarget target = await new SelectBattleTargetHandler(emptyCountries, armyUnits).Handle();
                BattleCountryChangeEvent battleCountryChange = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                battleCountryChange.IsTrigger = true;
                return battleCountryChange;
            })
            .WithCondition(()=> Condition.Build(new Condition.HasLandBattleTarget(Faction), this))
            .WithGuidance("Select a army or empty land country to attack")
        }; 
    }
}
