using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Grants SuppliedForTurn to a list of units in a network-safe way.
/// Used by Truk (and any future card that grants temporary supply to friendly pieces).
/// </summary>
public partial class GrantSupplyChangeEvent : ChangeEvent
{
    public List<int> UnitIds { get; set; }

    public GrantSupplyChangeEvent(Faction triggeringFaction, List<int> unitIds) : base(triggeringFaction)
    {
        UnitIds = unitIds;
    }

    public override ChangeEventDto ToDto()
    {
        GrantSupplyChangeEventDto dto = ChangeEventDto.Build<GrantSupplyChangeEventDto>(this, Id);
        dto.UnitIds = UnitIds;
        return dto;
    }

    protected override async Task<bool> ExecuteAsync()
    {
        foreach (int unitId in UnitIds)
            UnitState.ForId(unitId).SuppliedForTurn = true;
        await Task.CompletedTask;
        return true;
    }

    public override string SummaryText() =>
        $"{TriggeringFaction} granted supply to {UnitIds.Count} unit(s) for this turn";
}
