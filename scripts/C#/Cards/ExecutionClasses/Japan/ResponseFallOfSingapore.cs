using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class ResponseFallOfSingapore : ResponseCardLogic
{
    CountryState SouthEastAsia = CountryState.ForEnum(Country.SouthEastAsia);
    CountryState SouthChinaSea = CountryState.ForEnum(Country.SouthChinaSea);

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> { Condition.Build(new Condition.FactionBattled(Faction).WithCountries([SouthEastAsia.Id]), this) };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {

            new CardStep(this, async() => {
                
                BattleTarget target = await new SelectBattleTargetHandler(CountryState.ForEnum(Country.SouthChinaSea).BattleTargets(Faction)).Handle();
                BattleCountryChangeEvent battleCountryChangeEvent = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                battleCountryChangeEvent.IsTrigger = true;
                return battleCountryChangeEvent;
            })
            .WithGuidance("Battle in the South China Sea")
            .WithConditions( () => { return new List<Condition> { new Condition.CountryIsAttackable([CountryState.ForEnum(Country.SouthChinaSea).Id], Faction) }; } ),
            new CardStep(this, async() => {
                int selectedCountryId = await new SelectCountryHandler(
                        new List<int>{ CountryState.ForEnum(Country.SouthEastAsia).Id }
                    ).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                return deployUnitChangeEvent;
            }).WithConditions( ()=>{ return new List<Condition>{new Condition.CountryIsBuildable([CountryState.ForEnum(Country.SouthEastAsia).Id], Faction)}; } )
            .WithGuidance("Recruit an army in South East Asia")

        };
    }
}
