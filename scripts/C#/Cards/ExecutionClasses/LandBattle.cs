using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class LandBattle : CardLogic
{    
    /// <summary>
    /// A battle target is either an enemy army or an empty land country, so the offer has two halves.
    /// Both are read by the step's selection and by <see cref="Targets"/>, so the hover preview cannot
    /// drift from the real offer.
    /// </summary>
    private List<int> AttackableArmies => UnitState.AttackableArmyIds(Faction);

    /// <inheritdoc cref="AttackableArmies"/>
    private List<int> AttackableCountries => CountryState.AttackableLandIds(Faction);

    public override TargetSet Targets() =>
        TargetSet.Countries(AttackableCountries).Plus(TargetSet.Units(AttackableArmies));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                List<int> armyUnits = AttackableArmies;
                List<int> emptyCountries = AttackableCountries;
                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, emptyCountries, armyUnits).BroadCast();
                BattleTarget target = resp.ResponseCountryIds.Count > 0
                    ? new BattleTarget(resp.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);
                BattleCountryChangeEvent battleCountryChange = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                battleCountryChange.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(battleCountryChange);
            })
            .WithCondition(()=> Condition.Build(new Condition.HasLandBattleTarget(Faction), this))
            .WithGuidance("Select a army or empty land country to attack")
        }; 
    }
}
