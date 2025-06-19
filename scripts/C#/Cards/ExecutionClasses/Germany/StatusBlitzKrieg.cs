using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusBlitzkrieg : StatusCardLogic
{
    public override bool CanReactTo(ChangeEvent changeEvent)
    {
        return base.CanReactTo(changeEvent) &&
        changeEvent is BattleCountryChangeEvent battleCountryChangeEvent &&
        battleCountryChangeEvent.TriggeringFaction == Faction &&
        battleCountryChangeEvent.CountryState.Units.Keys.Count == 0;
    }

    public override List<CardStep> InitializeReactCardSteps()
    {
        return new List<CardStep> {
            new CardStep(this, async() => {
                List<BattleCountryChangeEvent> changeEvents = CardPlayPool.GetChangeEvents<BattleCountryChangeEvent>().Where(changeEvent=>changeEvent.CountryState.Units.Count == 0).ToList();
                int selectedCountryId = await new SelectCountryHandler(changeEvents.Map(changeEvent => changeEvent.CountryId)).Handle();

                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
                deployUnitChangeEvent.IsTrigger = true;
                CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
                IsActivationFinished = true;
            })
        };
    }
}
