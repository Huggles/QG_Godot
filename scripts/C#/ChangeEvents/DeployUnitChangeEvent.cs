using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class DeployUnitChangeEvent : ChangeEvent
{
    public int UnitId { get; set; }
    public int CountryId { get; set; }
    public DeployType DeploymentType { get; set; }

    public UnitState UnitState => UnitState.ForId(UnitId);
    public CountryState CountryState => CountryState.ForId(CountryId);

    public UnitType UnitType
    {
        get
        {
            CountryState country = CountryState.ForId(CountryId);
            return country.Type == CountryType.LAND ? UnitType.ARMY : UnitType.NAVY;
        }
    }

    public DeployUnitChangeEvent(Faction triggeringFaction, int countryId, DeployType deploymentType) : base(triggeringFaction)
    {
        CountryId = countryId;
        DeploymentType = deploymentType;
    }

    public override ChangeEventDto ToDto()
    {
        DeployUnitChangeEventDto dto = ChangeEventDto.Build<DeployUnitChangeEventDto>(this, Id);
        dto.CountryId = CountryId;
        dto.DeploymentType = DeploymentType;
        return dto;
    }

    protected override List<ChangeEventAnimation> BeforeAnimations => new()
    {
        new ZoomToCountryAnimation(CountryId),
    };

    protected override List<ChangeEventAnimation> AfterAnimations => new()
    {
        new ReturnCameraAnimation(),
    };

    protected override async Task<bool> ExecuteAsync()
    {
        GameAPI.DeployUnitToCountry(CountryId, TriggeringFaction, UnitType, DeploymentType, BlockAnimationQueue);
        await Task.CompletedTask;
        return true;
    }

    public override string TraceText()
    {
        return $"{ScriptName}-{Enum.GetName(typeof(Faction), TriggeringFaction)}-{CountryState.ForId(CountryId).Label}-{Enum.GetName(typeof(UnitType), UnitType)}";
    }

    public override string SummaryText()
    {
        return $"{UnitState.Faction} deployed to {CountryState.ForId(CountryId).Label}";
    }

    public override string DebugText()
    {
        return $"Deployed {Enum.GetName(typeof(Faction), TriggeringFaction)} {Enum.GetName(typeof(UnitType), UnitType)} to country: {CountryState.ForId(CountryId).Label}";
    }

    

}