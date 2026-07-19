using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class RemoveUnitChangeEvent : BattleCountryChangeEvent
{
    public int UnitId { get; private set; }
    public UnitRemovalReason Reason { get; private set; }

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
    }

    public override ChangeEventDto ToDto()
    {
        RemoveUnitChangeEventDto dto = ChangeEventDto.Build<RemoveUnitChangeEventDto>(this, Id);
        dto.UnitId = UnitId;
        dto.Reason = Reason;
        return dto;
    }

    protected override List<ChangeEventAnimation> AfterAnimations => new()
    {
        
    };

    protected override async Task<bool> ExecuteAsync()
    {
        GameAPI.RemoveUnitFromCountry(UnitId);     
        return true;
    }

    public override string SummaryText()
    {
        return $"Removed {UnitState.Faction} unit from {CountryState.ForId(CountryId).Label}";
    }

    public override string DebugText()
    {
        return $"{Faction.GetNames(typeof(Faction))[(int)TriggeringFaction]} removed unit from {CountryState.ForId(CountryId).Label}";
    }

    

    
}