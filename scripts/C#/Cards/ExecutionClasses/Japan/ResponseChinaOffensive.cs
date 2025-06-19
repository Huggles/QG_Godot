using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class ResponseChinaOffensive : ResponseCardLogic
{
    public List<CountryState> targetCountries = CountryState.ForEnums(new List<Country>
    {
        Country.China,
        Country.Vladivostok,
        Country.Mongolia,
        Country.Szechuan,
        Country.SouthEastAsia,
    });


    public override bool CanReactTo(ChangeEvent changeEvent)
    {
        return base.CanReactTo(changeEvent) &&
        changeEvent is BattleCountryChangeEvent battleCountryChangeEvent &&
        battleCountryChangeEvent.TriggeringFaction == Faction &&
        targetCountries.Contains(battleCountryChangeEvent.CountryState);
    }

    public override List<CardStep> InitializeReactCardSteps()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                int selectedCountryId = await new SelectCountryHandler(EligibleAttackedCountries()).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
                IsActivationFinished = true;
            }).WithConditions(
                ()=>{ return EligibleAttackedCountries().Map(countryId => new Condition.CountryIsEmpty(countryId)).ToList<Condition>(); }
            ),
            new CardStep(this, async() => {
                List<int> targets = targetCountries.Where(targetCountry=>targetCountry.CanAttack(Faction)).ToList().ToUnitIds();
                int selectedCountryId = await new SelectUnitHandler(targets).Handle();
                BattleUnitChangeEvent battleUnitChangeEvent = BuildChangeEvent(new BattleUnitChangeEvent(Faction, selectedCountryId));
                battleUnitChangeEvent.IsTrigger = true;
                CardPlayPool.DoChangeEvent(battleUnitChangeEvent);
            }).WithConditions(
                ()=>{ return EligibleAttackedCountries().Map(countryId => new Condition.CountryIsAttackable(countryId, Faction)).ToList<Condition>(); }
            ),
        };
    }

    private List<int> EligibleAttackedCountries()
    {
        List<int> attackedCountries = CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>()
                    .Where(
                        changeEvent => changeEvent.TriggeringFaction == Faction &&
                        targetCountries.Contains(changeEvent.CountryState)).ToList()
                        .Map(changeEvent => changeEvent.CountryId).ToList();
        return attackedCountries;
    }
}
