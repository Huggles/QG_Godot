using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class SelectCountryHandler : IGameEventHandler<int>
{
    List<int> countryIds;

    /// <summary>
    /// The faction being asked. Only used to find its own units standing on the offered countries — a
    /// country it already occupies is a legal deploy target ("build that army again"), and the marker
    /// for that goes on the unit rather than only on the empty country slot. Faction.NONE skips it.
    /// </summary>
    Faction selectingFaction;

    public SelectCountryHandler(List<int> countryIds, Faction selectingFaction = Faction.NONE)
    {
        this.countryIds = countryIds;
        this.selectingFaction = selectingFaction;
    }
    public SelectCountryHandler(List<Country> countryIds, Faction selectingFaction = Faction.NONE)
    {
        this.countryIds = countryIds.Map(countryEnum => (int)countryEnum);
        this.selectingFaction = selectingFaction;
    }

    /// <summary>
    /// The asked faction's own units standing on the offered countries. Clicking one answers with its
    /// country (see UnitScene.OnMouseLeftClickOpaque), so this list is purely "which units are also a
    /// way to point at an offered country".
    /// </summary>
    private List<int> RebuildTargetUnitIds()
    {
        if (selectingFaction == Faction.NONE) return new List<int>();

        return CountryState.ForIds(countryIds)
            .Where(country => country.HasUnit(selectingFaction))
            .Select(country => country.Units[selectingFaction])
            .ToList();
    }

    public async Task<int> Handle()
    {
        var tcs = new TaskCompletionSource<int>();
        void onCountry(int id) { tcs.TrySetResult(id); }
        void onSkip() { tcs.TrySetResult(-1); }

        // Captured up front: the deploy that follows can move units, and the finally below has to clear
        // the tag off exactly the units it was raised on.
        List<int> rebuildTargetUnitIds = RebuildTargetUnitIds();

        CountryState.ForIds(countryIds).AddTag(Tag.Clickable, Faction.ALL);
        UnitState.ForIds(rebuildTargetUnitIds).AddTag(Tag.RebuildTarget, Faction.ALL);
        InputManager.Current.EnableRayTraceCasting();
        SelectionSkipButton.Current?.Show();

        EventBus.Instance.CountryClicked += onCountry;
        EventBus.Instance.SelectionSkipped += onSkip;

        // Registered so error recovery can release this selection — the TCS is otherwise only ever
        // completed by a player click, so an aborted step would leave the board stuck as clickable.
        int countryId;
        using (PendingLocalInput.Register(onSkip))
        {
            try
            {
                countryId = await tcs.Task;
            }
            finally
            {
                // In a finally: previously a throw here permanently leaked Tag.Clickable, ray-trace
                // casting, the skip button and both event subscriptions.
                EventBus.Instance.CountryClicked -= onCountry;
                EventBus.Instance.SelectionSkipped -= onSkip;
                SelectionSkipButton.Current?.Hide();
                CountryState.ForIds(countryIds).RemoveTag(Tag.Clickable, Faction.ALL);
                UnitState.ForIds(rebuildTargetUnitIds).RemoveTag(Tag.RebuildTarget, Faction.ALL);
                InputManager.Current.DisableRayTraceCasting();
            }
        }

        if (countryId == -1)
        {
            DebugUtilities.PrintPeer("Country selection skipped");
            return -1;
        }

        DebugUtilities.PrintPeer($"Country Clicked: {CountryState.ForId(countryId).StaticCountryData.UniqueNameCamelCase}");
        return countryId;
    }
}
