using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ResponseSurpriseAttack : ResponseCardLogic
{
    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { 
            Condition.Build(new Condition.HasBattledAtSea(Faction), this) 
        };
    }

    public override List<CardStep> InitializeReactCardSteps()
    {
        return new List<CardStep> {
            // Battle a sea space
            new CardStep(this, async() => {
                List<int> navyUnits = UnitState.AttackableNavyIds(Faction);
                List<int> emptyCountries = CountryState.AttackableSeaIds(Faction);
                BattleTarget target = await new SelectBattleTargetHandler(emptyCountries, navyUnits).Handle();
                BattleCountryChangeEvent battleCountryChange = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                return battleCountryChange;
            })
            .WithCondition(()=> Condition.Build(new Condition.HasSeaBattleTarget(Faction), this))
            .WithGuidance("Battle a sea space"),
            
            // Battle a land space
            new CardStep(this, async() => {
                List<int> armyUnits = UnitState.AttackableArmyIds(Faction);
                List<int> emptyCountries = CountryState.AttackableLandIds(Faction);
                BattleTarget target = await new SelectBattleTargetHandler(emptyCountries, armyUnits).Handle();
                BattleCountryChangeEvent battleCountryChange = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                return battleCountryChange;
            })
            .WithCondition(()=> Condition.Build(new Condition.HasLandBattleTarget(Faction), this))
            .WithGuidance("Battle a land space"),
        }; 
    }
}