using Godot;
using System;
using System.Threading.Tasks;

public partial class BattleUnitChangeEvent : RemoveUnitChangeEvent
{
    public BattleUnitChangeEvent(Faction triggeringFaction, int unitId) : base(triggeringFaction, unitId, UnitRemovalReason.BATTLE)
    {
    }

    public override string SummaryText()
    {
        return $"Battled {UnitState.Faction} unit in {CountryState.ForId(CountryId).Label}";
    }

    public override string DebugText()
    {
        return $"{Faction.GetNames(typeof(Faction))[(int)TriggeringFaction]} battled {UnitState.ForId(UnitId).Faction} in {CountryState.ForId(CountryId).Label}";
    }
}