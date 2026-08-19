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
    /// Offered countries the asked faction already occupies — picking one rebuilds the piece standing
    /// there rather than placing a new one. Empty when no faction was supplied.
    /// </summary>
    private List<CountryState> RebuildTargetCountries()
    {
        if (selectingFaction == Faction.NONE) return new List<CountryState>();

        return CountryState.ForIds(countryIds)
            .Where(country => country.HasUnit(selectingFaction))
            .ToList();
    }

    public async Task<int> Handle()
    {
        var tcs = new TaskCompletionSource<int>();
        void onCountry(int id) { tcs.TrySetResult(id); }
        void onSkip() { tcs.TrySetResult(-1); }

        // Captured up front: the deploy that follows can move units, and the finally below has to clear
        // the tags off exactly the countries and units they were raised on.
        List<CountryState> rebuildCountries = RebuildTargetCountries();
        List<int> rebuildUnitIds = rebuildCountries.Select(country => country.Units[selectingFaction]).ToList();

        // RebuildTarget BEFORE Clickable: adding Clickable is what makes CountryScene draw the marker,
        // and it reads this tag to decide whether to draw the ordinary or the subdued one.
        rebuildCountries.AddTag(Tag.RebuildTarget, Faction.ALL);
        CountryState.ForIds(countryIds).AddTag(Tag.Clickable, Faction.ALL);
        UnitState.ForIds(rebuildUnitIds).AddTag(Tag.RebuildTarget, Faction.ALL);

        if (rebuildCountries.Count > 0)
        {
            DebugUtilities.PrintPeer(
                $"Country selection for {selectingFaction}: {rebuildCountries.Count} of {countryIds.Count} " +
                $"offered countries are rebuild-in-place targets " +
                $"({string.Join(", ", rebuildCountries.Select(country => country.Label))})");
        }
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
                // Clickable first: CountryScene.OnTagRemoved restyles a country that loses RebuildTarget
                // while still clickable, so clearing them the other way round would repaint every
                // rebuild target as an ordinary one on the way out.
                CountryState.ForIds(countryIds).RemoveTag(Tag.Clickable, Faction.ALL);
                rebuildCountries.RemoveTag(Tag.RebuildTarget, Faction.ALL);
                UnitState.ForIds(rebuildUnitIds).RemoveTag(Tag.RebuildTarget, Faction.ALL);
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
