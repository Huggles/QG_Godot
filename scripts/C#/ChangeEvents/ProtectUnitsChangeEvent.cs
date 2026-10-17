using System.Collections.Generic;

/// <summary>
/// "Do not remove that piece this turn": sets ImmuneForTurn on a list of units, as an event so every peer,
/// a rejoin and a save replay all get it (a direct write reached the host only, and the next event's hash
/// check reported a desync). ChangeRoundChangeEvent clears it when the turn ends.
///
/// A mechanical mutation rather than a game event (IsTrigger and RegisterInPool false): nothing reacts to
/// a protection, and the removal it answers stays the round's last event, which orders the reactions.
/// </summary>
public partial class ProtectUnitsChangeEvent : ChangeEvent
{
    public List<int> UnitIds { get; set; }

    public ProtectUnitsChangeEvent(Faction triggeringFaction, List<int> unitIds) : base(triggeringFaction)
    {
        UnitIds = unitIds;
        IsTrigger = false;
        RegisterInPool = false;
    }

    public override ChangeEventDto ToDto()
    {
        ProtectUnitsChangeEventDto dto = ChangeEventDto.Build<ProtectUnitsChangeEventDto>(this, Id);
        dto.UnitIds = UnitIds;
        return dto;
    }

    public override void Mutate(BoardState board)
    {
        foreach (int unitId in UnitIds)
            board.ForUnit(unitId).ImmuneForTurn = true;
    }

    /// <summary>Board: protected units drop out of the attack options and the Attackable tags.</summary>
    public override RecalcScope RecalcScope => RecalcScope.Board;

    public override string SummaryText() =>
        $"{TriggeringFaction.WithPlayer()} protected {UnitIds.Count} unit(s) from removal this turn";
}
