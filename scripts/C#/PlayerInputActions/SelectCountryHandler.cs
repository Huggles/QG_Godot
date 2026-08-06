using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class SelectCountryHandler : IGameEventHandler<int>
{
    List<int> countryIds;
    public SelectCountryHandler(List<int> countryIds)
    {
        this.countryIds = countryIds;
    }
    public SelectCountryHandler(List<Country> countryIds)
    {
        this.countryIds = countryIds.Map(countryEnum => (int)countryEnum);
    }

    public async Task<int> Handle()
    {
        var tcs = new TaskCompletionSource<int>();
        void onCountry(int id) { tcs.TrySetResult(id); }
        void onSkip() { tcs.TrySetResult(-1); }

        CountryState.ForIds(countryIds).AddTag(Tag.Clickable, Faction.ALL);
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
