using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class DeployUnitCardStep : CardStep
{
    public DeployUnitCardStep(CardLogic cardLogic, Func<Task<ChangeEvent>> stepLogic) : base(cardLogic, stepLogic)
    {
    }


    public async Task<ChangeEvent> Execute(List<int> countries)
    {
        int selectedCountryId = await new SelectCountryHandler(countries).Handle();
        DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(TriggeringFaction, selectedCountryId, DeployType.BUILD));
        deployUnitChangeEvent.IsTrigger = true;
        return deployUnitChangeEvent;
    }

}
