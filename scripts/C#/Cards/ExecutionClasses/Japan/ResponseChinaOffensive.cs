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

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.FactionBattled(Faction).Immediately().WithCountries(TargetCountries.ToCountryIds()), this) };
    }
 
    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, EligibleAttackedCountries()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithConditions(()=>{ return EligibleAttackedCountries().Map(countryId => Condition.Build(new Condition.CountryIsEmpty(countryId), this)).ToList<Condition>(); })
            .WithGuidance("Build an army in the country just battled"), 

            new CardStep(this, async() => {
                List<int> targets = TargetCountries.Where(targetCountry=>targetCountry.CanAttack(Faction)).ToList().ToUnitIds();
                int selectedCountryId = (await new InputRequest.SelectUnitRequestHandler(Faction, targets).BroadCast()).ResponseUnitIds[0];
                BattleUnitChangeEvent battleUnitChangeEvent = BuildChangeEvent(new BattleUnitChangeEvent(Faction, selectedCountryId));
                battleUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(battleUnitChangeEvent);
            }).WithCondition(
                ()=>{ return Condition.Build(new Condition.CountryIsAttackable(TargetCountries.ToCountryIds(), Faction), this); }
            ).WithGuidance("Attack an army in China or an adjacent country"),
        };
    }

    private List<int> EligibleAttackedCountries()
    {
        if (CardPlayPool.CurrentReactionTrigger is BattleCountryChangeEvent trigger
            && trigger.TriggeringFaction == Faction
            && TargetCountries.Contains(trigger.CountryState))
        {
            return new List<int> { trigger.CountryId };
        }
        return new List<int>();
    }
}
