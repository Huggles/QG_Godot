using Godot;
using System;
using System.Threading.Tasks;

public partial class RemoveUnitChangeEvent : ChangeEvent
{
    public int UnitId { get; private set; }
    public int CountryId { get; private set; }
    public UnitRemovalReason Reason { get; private set; }

    public UnitState UnitState => UnitState.ForId(UnitId);

    public RemoveUnitChangeEvent(Faction triggeringFaction, int unitId, UnitRemovalReason removalReason) : base(triggeringFaction)
    {
        UnitId = unitId;
        CountryId = UnitState.ForId(UnitId).CountryId;
        Reason = removalReason;
    }

    protected override async Task<bool> ExecuteAsync()
    {
        GameSession.RemoveUnitFromCountry(UnitId);
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