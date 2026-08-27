using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class DeployUnitChangeEvent : ChangeEvent
{
    /// <summary>
    /// The deployed unit, or -1 until <see cref="ExecuteAsync"/> has created it — a block window runs
    /// before that and has no unit to name yet.
    ///
    /// -1 rather than the default 0: unit ids start at 1 (<c>UnitPool.GetUniqueUnitId</c>
    /// pre-increments), so 0 was only accidentally distinguishable from a real id, and
    /// <see cref="Targets"/> has to tell the two apart.
    /// </summary>
    public int UnitId { get; set; } = -1;
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

    /// <summary>
    /// The country, plus the deployed unit once there is one. A BLOCK window runs before
    /// <see cref="ExecuteAsync"/>, so it gets the country alone; an AFTER-REACTION window runs after
    /// it and can point straight at the new unit.
    /// </summary>
    public override TargetSet Targets() =>
        UnitId > -1
            ? TargetSet.Countries(new[] { CountryId }).Plus(TargetSet.Units(new[] { UnitId }))
            : TargetSet.Countries(new[] { CountryId });

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
        
        UnitId = GameAPI.DeployUnitToCountry(CountryId, TriggeringFaction, UnitType, DeploymentType, BlockAnimationQueue);
        await Task.CompletedTask;
        return true;
    }

    public override string TraceText()
    {
        return $"{ScriptName}-{Enum.GetName(typeof(Faction), TriggeringFaction)}-{CountryState.ForId(CountryId).Label}-{Enum.GetName(typeof(UnitType), UnitType)}";
    }

    public override string SummaryText() => $"{TriggeringFaction.WithPlayer()} deployed to {CountryState.ForId(CountryId).Label}";
    public override string DebugText() => $"Deployed {Enum.GetName(typeof(Faction), TriggeringFaction)} {Enum.GetName(typeof(UnitType), UnitType)} to country: {CountryState.ForId(CountryId).Label}";
}