using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class ResponseChinaOffensive : ResponseCardLogic
{
    private List<CountryState> TargetCountries => CountryState.ForEnums(new List<Country>
    {
        Country.China,
        Country.Vladivostok,
        Country.Mongolia,
        Country.Szechuan,
        Country.SouthEastAsia,
    });

    private List<BattleTarget> BattleTargets => BattleTarget.In(TargetCountries.ToCountryIds(), Faction);

    /// <summary>The space just battled, where step 1 builds, plus what step 2 may attack.</summary>
    public override TargetSet Targets() =>
        TargetSet.Countries(EligibleAttackedCountries())
            .Plus(TargetSet.FromBattleTargets(BattleTargets));

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.FactionBattled(Faction).Immediately().WithCountries(TargetCountries.ToCountryIds()), this) };
    }
 
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new ResultStep(this, Choose.CountryFrom(() => EligibleAttackedCountries(),
                countryId => new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD)))
            .WithConditions(()=>{ return EligibleAttackedCountries().Map(countryId => Condition.Build(new Condition.CountryIsEmpty(countryId), this)).ToList<Condition>(); })
            .WithGuidance("Build an army in the country just battled"), 

            new ResultStep(this, Choose.BattleTargetFrom(() => BattleTargets,
                target => target.ToAttackChangeEvent(Faction)))
            .WithCondition(
                ()=>{ return Condition.Build(new Condition.CountryIsAttackable(TargetCountries.ToCountryIds(), Faction), this); }
            ).WithGuidance("Battle in China or an adjacent land space"),
        };
    }

    private List<int> EligibleAttackedCountries()
    {
        if (TriggerContext is BattleCountryChangeEvent trigger
            && trigger.TriggeringFaction == Faction
            && TargetCountries.Contains(trigger.CountryState))
        {
            return new List<int> { trigger.CountryId };
        }
        return new List<int>();
    }
}
