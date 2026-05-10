using Godot;
using System;
using System.Threading.Tasks;

public partial class RemoveUnitChangeEvent : BattleCountryChangeEvent
{
    public int UnitId { get; private set; }
    public UnitRemovalReason Reason { get; private set; }

    public UnitState UnitState => UnitState.ForId(UnitId);

    public RemoveUnitChangeEvent(Faction triggeringFaction, int unitId, UnitRemovalReason removalReason) : base(triggeringFaction, UnitState.ForId(unitId).CountryId)
    {
        UnitId = unitId;        
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