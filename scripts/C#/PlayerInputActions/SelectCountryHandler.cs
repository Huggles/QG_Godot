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
        CountryState.ForIds(countryIds).AddTag(Tag.Clickable, Faction.ALL);
        InputManager.Instance.EnableRayTraceCasting();
        int countryId = (await EventBus.GetSignalAwaiter(EventBus.SignalName.CountryClicked))[0].As<int>();
        CountryState.ForIds(countryIds).RemoveTag(Tag.Clickable, Faction.ALL);
        InputManager.Instance.DisableRayTraceCasting();
        DebugUtilities.PrintPeerError($"Country Clicked: {CountryState.ForId(countryId).StaticCountryData.UniqueNameCamelCase}");
        return countryId;
    }
}
