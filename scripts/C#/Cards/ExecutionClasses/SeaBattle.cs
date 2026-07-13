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
                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, emptyCountries, navyUnits).BroadCast();
                BattleTarget target = resp.ResponseCountryIds.Count > 0
                    ? new BattleTarget(resp.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);
                BattleCountryChangeEvent battleCountryChange = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                battleCountryChange.IsTrigger = true;
                return battleCountryChange;
            })
            .WithCondition(()=>Condition.Build(new Condition.HasSeaBattleTarget(Faction), this))            
            .WithGuidance("Select a navy or empty sea country to attack")
        }; 
    }
}
