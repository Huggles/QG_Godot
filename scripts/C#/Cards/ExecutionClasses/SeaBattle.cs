using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class SeaBattle : CardLogic
{
    /// <summary>
    /// A battle target is either an enemy navy or an empty sea country, so the offer has two halves.
    /// Both are read by the step's selection and by <see cref="Targets"/>, so the hover preview cannot
    /// drift from the real offer.
    /// </summary>
    private List<int> AttackableNavies => UnitState.AttackableNavyIds(Faction);

    /// <inheritdoc cref="AttackableNavies"/>
    private List<int> AttackableCountries => CountryState.AttackableSeaIds(Faction);

    public override TargetSet Targets() =>
        TargetSet.Countries(AttackableCountries).Plus(TargetSet.Units(AttackableNavies));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                List<int> navyUnits = AttackableNavies;
                List<int> emptyCountries = AttackableCountries;
                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, emptyCountries, navyUnits).BroadCast();
                BattleTarget target = resp.ResponseCountryIds.Count > 0
                    ? new BattleTarget(resp.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);
                BattleCountryChangeEvent battleCountryChange = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                battleCountryChange.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(battleCountryChange);
            })
            .WithCondition(()=>Condition.Build(new Condition.HasSeaBattleTarget(Faction), this))
            .WithGuidance("Select a navy or empty sea country to attack")
        }; 
    }
}
