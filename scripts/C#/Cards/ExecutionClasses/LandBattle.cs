using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class LandBattle : CardLogic
{    
    public override List<CardStep> OnActivate()
    {
        // Hoisted so the step's own selection and the hover preview cannot drift apart — see
        // CardStep.WithTargetPreview. A battle target is either an enemy army or an empty land
        // country, so both halves are declared and the preview lights the countries either way.
        Func<List<int>> attackableArmies = () => UnitState.AttackableArmyIds(Faction);
        Func<List<int>> attackableCountries = () => CountryState.AttackableLandIds(Faction);

        return new List<CardStep> {
            new CardStep(this, async() => {
                List<int> armyUnits = attackableArmies();
                List<int> emptyCountries = attackableCountries();
                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, emptyCountries, armyUnits).BroadCast();
                BattleTarget target = resp.ResponseCountryIds.Count > 0
                    ? new BattleTarget(resp.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);
                BattleCountryChangeEvent battleCountryChange = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                battleCountryChange.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(battleCountryChange);
            })
            .WithTargetPreview(()=> StepTargetPreview.Both(attackableCountries(), attackableArmies()))
            .WithCondition(()=> Condition.Build(new Condition.HasLandBattleTarget(Faction), this))
            .WithGuidance("Select a army or empty land country to attack")
        }; 
    }
}
