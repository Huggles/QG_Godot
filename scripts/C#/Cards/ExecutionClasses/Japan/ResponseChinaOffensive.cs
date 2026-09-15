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
            new ResultStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, EligibleAttackedCountries()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD);
                return deployUnitChangeEvent;
            })
            .WithConditions(()=>{ return EligibleAttackedCountries().Map(countryId => Condition.Build(new Condition.CountryIsEmpty(countryId), this)).ToList<Condition>(); })
            .WithGuidance("Build an army in the country just battled"), 

            new ResultStep(this, async() => {
                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, BattleTargets).BroadCast();
                BattleTarget target = resp.ResponseCountryIds.Count > 0
                    ? new BattleTarget(resp.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);
                BattleCountryChangeEvent battleEvent = target.ToAttackChangeEvent(Faction);
                return battleEvent;
            })
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
