using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class RemoveUnitChangeEvent : BattleCountryChangeEvent
{
    public int UnitId { get; private set; }
    public UnitRemovalReason Reason { get; private set; }

    /// <summary>
    /// Whether the unit was in supply at the moment the event was created (before ExecuteAsync removes it).
    /// Use this in after-reaction conditions instead of UnitState.InSupply, which is cleared by
    /// CalculateAll before reactions are checked.
    /// </summary>
    public bool WasInSupply { get; private set; }

    /// <summary>
    /// WARNING: UnitState.CountryId is set to -1 after ExecuteAsync runs (unit removed from country).
    /// Always use this event's inherited <see cref="BattleCountryChangeEvent.CountryId"/> to get the
    /// country — it is captured at construction time and remains valid after execution.
    /// </summary>
    public UnitState UnitState => UnitState.ForId(UnitId);

    public RemoveUnitChangeEvent(Faction triggeringFaction, int unitId, UnitRemovalReason removalReason) : base(triggeringFaction, UnitState.ForId(unitId).CountryId)
    {
        UnitId = unitId;        
        Reason = removalReason;
        WasInSupply = UnitState.ForId(unitId).InSupply;
    }

    public override ChangeEventDto ToDto()
    {
        RemoveUnitChangeEventDto dto = ChangeEventDto.Build<RemoveUnitChangeEventDto>(this, Id);
        dto.UnitId = UnitId;
        dto.Reason = Reason;
        return dto;
    }

    /// <summary>
    /// Battle and eliminate removals get the deploy-style camera zoom; supply attrition does not.
    /// </summary>
    private bool ShouldZoom => Reason != UnitRemovalReason.SUPPLY;

    protected override List<ChangeEventAnimation> BeforeAnimations =>
        ShouldZoom
            ? new() { new ZoomToCountryAnimation(CountryId) }
            : new();

    protected override List<ChangeEventAnimation> AfterAnimations =>
        ShouldZoom
            ? new() { new ReturnCameraAnimation() }
            : new();

    protected override async Task<bool> ExecuteAsync()
    {
        GameAPI.RemoveUnitFromCountry(UnitId);     
        return true;
    }

    public override string SummaryText()
    {
        return $"Removed {UnitState.Faction.Label()} unit from {CountryState.ForId(CountryId).Label}";
    }

    public override string DebugText()
    {
        return $"{Faction.GetNames(typeof(Faction))[(int)TriggeringFaction]} removed unit from {CountryState.ForId(CountryId).Label}";
    }

    

    
}