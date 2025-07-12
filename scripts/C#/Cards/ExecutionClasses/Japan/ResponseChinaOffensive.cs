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

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.FactionBattled(Faction).WithCountries(targetCountries.ToCountryIds()), this) };
    }

    public override List<CardStep> InitializeReactCardSteps()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                int selectedCountryId = await new SelectCountryHandler(EligibleAttackedCountries()).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                _ = CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
                IsActivationFinished = true;
            })
            .WithConditions(()=>{ return EligibleAttackedCountries().Map(countryId => Condition.Build(new Condition.CountryIsEmpty(countryId), this)).ToList<Condition>(); })
            .WithGuidance("Build an army in the country just battled"), 

            new CardStep(this, async() => {
                List<int> targets = targetCountries.Where(targetCountry=>targetCountry.CanAttack(Faction)).ToList().ToUnitIds();
                int selectedCountryId = await new SelectUnitHandler(targets).Handle();
                BattleUnitChangeEvent battleUnitChangeEvent = BuildChangeEvent(new BattleUnitChangeEvent(Faction, selectedCountryId));
                _ = CardPlayPool.DoChangeEvent(battleUnitChangeEvent);
            }).WithCondition(
                ()=>{ return Condition.Build(new Condition.CountryIsAttackable(targetCountries.ToCountryIds(),Faction), this); }
            ).WithGuidance("Attack an army in China or an adjacent country"),
        };
    }

    private List<int> EligibleAttackedCountries()
    {
        List<BattleCountryChangeEvent> battleCountryChangeEvents = CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>();
        battleCountryChangeEvents = battleCountryChangeEvents.Where(b => b.TriggeringFaction == Faction).ToList();
        battleCountryChangeEvents = battleCountryChangeEvents.Where(b => targetCountries.Contains(b.CountryState)).ToList();
        List<int> attackedCountries = battleCountryChangeEvents.Where(
                        changeEvent => changeEvent.TriggeringFaction == Faction &&
                        targetCountries.Contains(changeEvent.CountryState)).ToList()
                        .Map(changeEvent => changeEvent.CountryId).ToList();
        return attackedCountries;
    }
}
