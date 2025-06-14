using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class SelectUnitHandler : IGameEventHandler<int>
{
    List<int> unitIds;
    public SelectUnitHandler(List<int> unitIds)
    {
        this.unitIds = unitIds;
    }

    public async Task<int> Handle()
    {
        EventBus.Emit(EventBus.SignalName.SetUnitsClickable, unitIds.ToArray());
        int unitId = (await EventBus.GetSignalAwaiter(EventBus.SignalName.UnitClicked))[0].As<int>();
        EventBus.Emit(EventBus.SignalName.SetAllCountriesUnclickable);
        DebugUtilities.PrintPeerError($"Unit Clicked: {UnitState.ForId(unitId).CountryState.StaticCountryData.UniqueNameCamelCase} {UnitState.ForId(unitId).Faction}");
        return unitId;
    }
}
