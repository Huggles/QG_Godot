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

    public override ChangeEventDto ToDto() => new RemoveUnitChangeEventDto
    {
        TriggeringFaction = TriggeringFaction, SourceCardId = SourceCardId,
        IsTrigger = IsTrigger, SuppressGameProgress = SuppressGameProgress,
        UnitId = UnitId, Reason = Reason
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