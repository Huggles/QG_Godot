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
            Condition.Build(new Condition.HasBattledAtSea(Faction).Immediately(), this)
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Battle a sea space
            new CardStep(this, async() => {
                List<int> navyUnits = UnitState.AttackableNavyIds(Faction);
                List<int> emptyCountries = CountryState.AttackableSeaIds(Faction);
                var respSea = await new InputRequest.SelectBattleTargetRequestHandler(Faction, emptyCountries, navyUnits).BroadCast();
                BattleTarget target = respSea.ResponseCountryIds.Count > 0
                    ? new BattleTarget(respSea.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(respSea.ResponseUnitIds[0], TargetType.UNIT);
                BattleCountryChangeEvent battleCountryChange = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                return battleCountryChange;
            })
            .WithCondition(()=> Condition.Build(new Condition.HasSeaBattleTarget(Faction), this))
            .WithGuidance("Battle a sea space"),
            
            // Battle a land space
            new CardStep(this, async() => {
                List<int> armyUnits = UnitState.AttackableArmyIds(Faction);
                List<int> emptyCountries = CountryState.AttackableLandIds(Faction);
                var respLand = await new InputRequest.SelectBattleTargetRequestHandler(Faction, emptyCountries, armyUnits).BroadCast();
                BattleTarget target = respLand.ResponseCountryIds.Count > 0
                    ? new BattleTarget(respLand.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(respLand.ResponseUnitIds[0], TargetType.UNIT);
                BattleCountryChangeEvent battleCountryChange = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                return battleCountryChange;
            })
            .WithCondition(()=> Condition.Build(new Condition.HasLandBattleTarget(Faction), this))
            .WithGuidance("Battle a land space"),
        }; 
    }
}