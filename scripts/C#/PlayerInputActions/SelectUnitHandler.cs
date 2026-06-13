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
        UnitState.ForIds(unitIds).AddTag(Tag.Clickable, Faction.ALL);
        InputManager.Current.EnableRayTraceCasting();
        int unitId = (await EventBus.GetSignalAwaiter(EventBus.SignalName.UnitClicked))[0].As<int>();
        UnitState.ForIds(unitIds).RemoveTag(Tag.Clickable, Faction.ALL);
        InputManager.Current.DisableRayTraceCasting();
        DebugUtilities.PrintPeerError($"Unit Clicked: {UnitState.ForId(unitId).CountryState.StaticCountryData.UniqueNameCamelCase} {UnitState.ForId(unitId).Faction}");
        return unitId;
    }
}
