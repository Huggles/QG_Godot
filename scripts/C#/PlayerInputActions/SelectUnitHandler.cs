using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class SelectUnitHandler : IGameEventHandler<int>
{
    List<int> unitIds;
    private readonly bool allowSkip;

    /// <param name="allowSkip">
    /// False for a mandatory selection: the Skip button is not shown and the SelectionSkipped signal is
    /// not listened for, so the player cannot decline. Error recovery can still release the awaiter
    /// through PendingLocalInput — see below.
    /// </param>
    public SelectUnitHandler(List<int> unitIds, bool allowSkip = true)
    {
        this.unitIds = unitIds;
        this.allowSkip = allowSkip;
    }

    public async Task<int> Handle()
    {
        var tcs = new TaskCompletionSource<int>();
        void onUnit(int id) { tcs.TrySetResult(id); }
        void onSkip() { tcs.TrySetResult(-1); }

        UnitState.ForIds(unitIds).AddTag(Tag.Clickable, Faction.ALL);
        InputManager.Current.EnableRayTraceCasting();
        if (allowSkip) SelectionSkipButton.Current?.Show();

        EventBus.Instance.UnitClicked += onUnit;
        if (allowSkip) EventBus.Instance.SelectionSkipped += onSkip;

        // Registered so error recovery can release this selection — see SelectCountryHandler. Kept even
        // for a mandatory selection: ErrorReporter.CancelPendingAwaiters is how a failed step stops
        // waiting on a prompt nobody is going to answer, and losing that would deadlock the recovery.
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
                if (allowSkip)
                {
                    EventBus.Instance.SelectionSkipped -= onSkip;
                    SelectionSkipButton.Current?.Hide();
                }
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
