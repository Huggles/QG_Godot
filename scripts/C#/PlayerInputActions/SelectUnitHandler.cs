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
        var tcs = new TaskCompletionSource<int>();
        void onUnit(int id) { tcs.TrySetResult(id); }
        void onSkip() { tcs.TrySetResult(-1); }

        UnitState.ForIds(unitIds).AddTag(Tag.Clickable, Faction.ALL);
        InputManager.Current.EnableRayTraceCasting();
        SelectionSkipButton.Current?.Show();

        EventBus.Instance.UnitClicked += onUnit;
        EventBus.Instance.SelectionSkipped += onSkip;

        // Registered so error recovery can release this selection — see SelectCountryHandler.
        int unitId;
        using (PendingLocalInput.Register(onSkip))
        {
            try
            {
                unitId = await tcs.Task;
            }
            finally
            {
                EventBus.Instance.UnitClicked -= onUnit;
                EventBus.Instance.SelectionSkipped -= onSkip;
                SelectionSkipButton.Current?.Hide();
                UnitState.ForIds(unitIds).RemoveTag(Tag.Clickable, Faction.ALL);
                InputManager.Current.DisableRayTraceCasting();
            }
        }

        if (unitId == -1)
        {
            DebugUtilities.PrintPeer("Unit selection skipped");
            return -1;
        }

        DebugUtilities.PrintPeerError($"Unit Clicked: {UnitState.ForId(unitId).CountryState.StaticCountryData.UniqueNameCamelCase} {UnitState.ForId(unitId).Faction}");
        return unitId;
    }
}
