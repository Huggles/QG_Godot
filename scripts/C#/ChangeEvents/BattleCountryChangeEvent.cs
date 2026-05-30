using Godot;
using System;
using System.Threading.Tasks;

public partial class BattleCountryChangeEvent : ChangeEvent
{
    public int CountryId { get; private set; }
    public CountryState CountryState => CountryState.ForId(CountryId);


    public BattleCountryChangeEvent(Faction triggeringFaction, int countryId) : base(triggeringFaction)
    {
        CountryId = countryId;
    }

    public override ChangeEventDto ToDto() => new BattleCountryChangeEventDto
    {
        TriggeringFaction = TriggeringFaction, SourceCardId = SourceCardId,
        IsTrigger = IsTrigger, SuppressGameProgress = SuppressGameProgress,
        CountryId = CountryId
    };

    protected async override Task<bool> ExecuteAsync()
    {
        await Task.CompletedTask;
        return true;
    }

}