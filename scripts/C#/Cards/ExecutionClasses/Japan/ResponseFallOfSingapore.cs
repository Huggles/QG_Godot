using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class ResponseFallOfSingapore : StatusCardLogic
{
    public override bool CanReactTo(ChangeEvent changeEvent) 
    {
        return base.CanReactTo(changeEvent) && changeEvent is BattleCountryChangeEvent battleCountryChangeEvent && battleCountryChangeEvent.TriggeringFaction == Faction && battleCountryChangeEvent.CountryState == Country.SouthEastAsia;
    }

    public override List<CardStep> InitializeReactCardSteps()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                BattleTarget target = await new SelectBattleTargetHandler(CountryState.ForEnum(Country.SouthChinaSea).BattleTargets(Faction)).Handle();
                BattleCountryChangeEvent battleCountryChangeEvent = BuildChangeEvent(target.ToAttackChangeEvent(Faction));
                battleCountryChangeEvent.IsTrigger = true;
                CardPlayPool.DoChangeEvent(battleCountryChangeEvent);
            }),
            new CardStep(this, async() => {
                int selectedCountryId = await new SelectCountryHandler(
                        new List<int>{ CountryState.ForEnum(Country.SouthEastAsia).Id }
                    ).Handle();
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
                IsActivationFinished = true;
            }).WithConditions(
                ()=>{ return new List<Condition>{new Condition.CountryIsEmpty(CountryState.ForEnum(Country.SouthEastAsia).Id)}; }
            )

        };
    }
}
