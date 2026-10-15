using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>Why an Air Force moved. Appended to only: the value travels in saves and on the wire.</summary>
public enum AirForceMoveReason
{
    /// <summary>Its faction spent the Air step moving it to any space with its own supplied Army or Navy.</summary>
    MARSHAL,
    /// <summary>The Army or Navy it stood with was removed, so it moved to an adjacent one. See AirForces.</summary>
    REPOSITION
}

/// <summary>
/// Moves an Air Force to another space, keeping the same piece, so it can be seen flying there rather
/// than burning in one space and dropping into another. Ground units never move in this game; only an
/// Air Force does, when marshalled or repositioned.
/// </summary>
public partial class MoveAirForceChangeEvent : ChangeEvent
{
    public int UnitId { get; set; }
    public int FromCountryId { get; set; }
    public int ToCountryId { get; set; }
    public AirForceMoveReason Reason { get; set; }

    public UnitState UnitState => UnitState.ForId(UnitId);

    /// <summary>The origin is read off <paramref name="board"/> now: after Mutate the unit is elsewhere.</summary>
    public MoveAirForceChangeEvent(Faction triggeringFaction, int unitId, int toCountryId, AirForceMoveReason reason, BoardState board = null)
        : this(triggeringFaction, unitId, (board ?? BoardState.Live).CountryOf(UnitState.ForId(unitId)), toCountryId, reason) { }

    /// <summary>With an explicit origin: how a receiving peer rebuilds the event from its DTO.</summary>
    public MoveAirForceChangeEvent(Faction triggeringFaction, int unitId, int fromCountryId, int toCountryId, AirForceMoveReason reason)
        : base(triggeringFaction)
    {
        UnitId = unitId;
        FromCountryId = fromCountryId;
        ToCountryId = toCountryId;
        Reason = reason;
    }

    public override ChangeEventDto ToDto()
    {
        MoveAirForceChangeEventDto dto = ChangeEventDto.Build<MoveAirForceChangeEventDto>(this, Id);
        dto.UnitId = UnitId;
        dto.FromCountryId = FromCountryId;
        dto.ToCountryId = ToCountryId;
        dto.Reason = Reason;
        return dto;
    }

    public override TargetSet Targets() =>
        TargetSet.Countries(new[] { FromCountryId, ToCountryId }).Plus(TargetSet.Units(new[] { UnitId }));

    /// <summary>Only where a piece stands changed: no card piles, no flow.</summary>
    public override RecalcScope RecalcScope => RecalcScope.Board;

    public override void Mutate(BoardState board) => board.MoveAirForce(UnitId, ToCountryId);

    protected override Task OnLiveMutatedAsync()
    {
        GameAPI.PresentMove(UnitId, FromCountryId, ToCountryId, BlockAnimationQueue);
        return Task.CompletedTask;
    }

    /// <summary>Never throws: it goes on the wire as InputRequest.TriggerSummaryText.</summary>
    public override string SummaryText()
    {
        Faction owner = GameSession.Current != null ? UnitState.Faction : TriggeringFaction;
        string from = GameSession.Current != null ? CountryState.ForId(FromCountryId)?.Label : null;
        string to = GameSession.Current != null ? CountryState.ForId(ToCountryId)?.Label : null;
        string verb = Reason == AirForceMoveReason.MARSHAL ? "marshalled" : "repositioned";
        return $"{owner.Label()} Air Force {verb} from {from ?? $"country {FromCountryId}"} to {to ?? $"country {ToCountryId}"}";
    }
}
