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
    /// -1 rather than the default 0: unit ids start at 0 (<c>UnitPool.GetUniqueUnitId</c>
    /// pre-increments from -1), so 0 is a real id that <see cref="Targets"/> has to be able to tell
    /// apart from "no unit yet".
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

    /// <summary>
    /// "Germany (Bob) built an Army in India" — how the unit arrived, not just that it did.
    ///
    /// The verb comes from <see cref="DeploymentType"/> and the noun from <see cref="UnitType"/>,
    /// because a reaction window shows this line as the event being blocked or answered and "deployed
    /// to India" left the player to guess whether they were looking at a build or a recruit.
    ///
    /// Falls back to the bare "deployed to {country}" if the country does not resolve: UnitType reads
    /// CountryState.ForId(CountryId).Type, and SummaryText() goes on the wire as
    /// InputRequest.TriggerSummaryText, so this must never throw — the same defence
    /// <c>GameMessageDisplay.DeployIcon</c> documents.
    /// </summary>
    public override string SummaryText()
    {
        CountryState country = GameSession.Current != null ? CountryState.ForId(CountryId) : null;
        if (country == null) return $"{TriggeringFaction.WithPlayer()} deployed to country {CountryId}";

        string verb = DeploymentType switch
        {
            DeployType.BUILD   => "built",
            DeployType.RECRUIT => "recruited",
            _                  => "deployed",
        };
        string unit = UnitType == UnitType.NAVY ? "a Navy" : "an Army";
        return $"{TriggeringFaction.WithPlayer()} {verb} {unit} in {country.Label}";
    }

    public override string DebugText() => $"Deployed {Enum.GetName(typeof(Faction), TriggeringFaction)} {Enum.GetName(typeof(UnitType), UnitType)} to country: {CountryState.ForId(CountryId).Label}";
}