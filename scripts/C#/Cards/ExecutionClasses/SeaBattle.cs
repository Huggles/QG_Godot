using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class SeaBattle : CardLogic
{
    public override List<CardStep> OnActivate()
    {
        // Hoisted so the step's own selection and the hover preview cannot drift apart — see
        // CardStep.WithTargetPreview. A battle target is either an enemy navy or an empty sea
        // country, so both halves are declared and the preview lights the countries either way.
        Func<List<int>> attackableNavies = () => UnitState.AttackableNavyIds(Faction);
        Func<List<int>> attackableCountries = () => CountryState.AttackableSeaIds(Faction);

        return new List<CardStep> {
            new CardStep(this, async() => {
                List<int> navyUnits = attackableNavies();
                List<int> emptyCountries = attackableCountries();
                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, emptyCountries, navyUnits).BroadCast();
                BattleTarget target = resp.ResponseCountryIds.Count > 0
                    ? new BattleTarget(resp.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);
                BattleCountryChangeEvent battleCountryChange = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                battleCountryChange.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(battleCountryChange);
            })
            .WithTargetPreview(()=> StepTargetPreview.Both(attackableCountries(), attackableNavies()))
            .WithCondition(()=>Condition.Build(new Condition.HasSeaBattleTarget(Faction), this))
            .WithGuidance("Select a navy or empty sea country to attack")
        }; 
    }
}
