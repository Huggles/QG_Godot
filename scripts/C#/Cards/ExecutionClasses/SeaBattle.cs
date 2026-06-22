using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class SeaBattle : CardLogic
{
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                List<int> navyUnits = UnitState.AttackableNavyIds(Faction);
                List<int> emptyCountries = CountryState.AttackableSeaIds(Faction);
                BattleTarget target = await new SelectBattleTargetHandler(emptyCountries, navyUnits).Handle();
                BattleCountryChangeEvent battleCountryChange = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                battleCountryChange.IsTrigger = true;
                return battleCountryChange;
            })
            .WithCondition(()=>Condition.Build(new Condition.HasSeaBattleTarget(Faction), this))            
            .WithGuidance("Select a navy or empty sea country to attack")
        }; 
    }
}
