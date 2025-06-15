using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StatusBlitzkrieg : StatusCardLogic
{
    public override bool CanReactTo(ChangeEvent changeEvent)
    {
        return changeEvent is BattleUnitChangeEvent battleUnitChangeEvent && battleUnitChangeEvent.TriggeringFaction == Faction.GERMANY && battleUnitChangeEvent.CountryState.Units.Keys.Count == 0;
    }
    public async override void InitialReactStep()
    {
        List<BattleUnitChangeEvent> changeEvents = CardPlayPool.GetChangeEvents<BattleUnitChangeEvent>();
        int selectedCountryId = await new SelectCountryHandler(changeEvents.Map(changeEvent => changeEvent.CountryId)).Handle();

        DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
        deployUnitChangeEvent.IsTrigger = true;
        CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
        IsActivationFinished = true;
    }
}
