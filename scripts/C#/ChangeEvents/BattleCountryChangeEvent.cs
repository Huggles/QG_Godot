using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class BattleCountryChangeEvent : ChangeEvent
{
    public int CountryId { get; private set; }
    public CountryState CountryState => CountryState.ForId(CountryId);


    public BattleCountryChangeEvent(Faction triggeringFaction, int countryId) : base(triggeringFaction)
    {
        CountryId = countryId;
    }

    protected override List<ChangeEventAnimation> BeforeAnimations => new()
    {
        new ZoomToCountryAnimation(CountryId),
    };

    protected override List<ChangeEventAnimation> AfterAnimations => new()
    {
        new ReturnCameraAnimation(),
    };

    public override ChangeEventDto ToDto()
    {
        BattleCountryChangeEventDto dto = ChangeEventDto.Build<BattleCountryChangeEventDto>(this, Id);
        dto.CountryId = CountryId;
        return dto;
    }

    protected async override Task<bool> ExecuteAsync()
    {
        await Task.CompletedTask;
        return true;
    }

}