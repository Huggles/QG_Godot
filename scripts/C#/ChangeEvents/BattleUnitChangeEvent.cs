using Godot;
using System;
using System.Threading.Tasks;

public partial class BattleUnitChangeEvent : RemoveUnitChangeEvent
{
    public BattleUnitChangeEvent(Faction triggeringFaction, int unitId) : base(triggeringFaction, unitId, UnitRemovalReason.BATTLE)
    {
    }

    public override ChangeEventDto ToDto()
    {
        BattleUnitChangeEventDto dto = ChangeEventDto.Build<BattleUnitChangeEventDto>(this, Id);
        dto.UnitId = UnitId;
        return dto;
    }

    // The attacker is named with its player; the defending unit's faction is adjectival ("a Germany
    // unit"), so it stays a plain label — naming both players makes this row unreadably long.
    public override string SummaryText() => $"{TriggeringFaction.WithPlayer()} battled {UnitState.Faction.Label()} unit in {CountryState.ForId(CountryId).Label}";
    public override string DebugText() => $"{Faction.GetNames(typeof(Faction))[(int)TriggeringFaction]} battled {UnitState.ForId(UnitId).Faction} in {CountryState.ForId(CountryId).Label}";
}