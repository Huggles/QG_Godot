using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class RemoveUnitChangeEvent : BattleCountryChangeEvent
{
    public int UnitId { get; private set; }
    public UnitRemovalReason Reason { get; private set; }

    /// <summary>
    /// Whether the unit was in supply at the moment the event was created (before Mutate removes it).
    /// Use this in after-reaction conditions instead of UnitState.InSupply, which is cleared by
    /// CalculateAll before reactions are checked.
    /// </summary>
    public bool WasInSupply { get; private set; }

    /// <summary>
    /// WARNING: UnitState.CountryId is set to -1 after Mutate runs (unit removed from country).
    /// Always use this event's inherited <see cref="BattleCountryChangeEvent.CountryId"/> to get the
    /// country — it is captured at construction time and remains valid after execution.
    /// </summary>
    public UnitState UnitState => UnitState.ForId(UnitId);

    /// <summary>The unit's country and supply are read off <paramref name="board"/>: the live one unless an outcome is built for a fork.</summary>
    public RemoveUnitChangeEvent(Faction triggeringFaction, int unitId, UnitRemovalReason removalReason, BoardState board = null)
        : base(triggeringFaction, (board ?? BoardState.Live).CountryOf(UnitState.ForId(unitId)))
    {
        UnitId = unitId;        
        Reason = removalReason;
        WasInSupply = (board ?? BoardState.Live).InSupply(UnitState.ForId(unitId));
    }

    public override ChangeEventDto ToDto()
    {
        RemoveUnitChangeEventDto dto = ChangeEventDto.Build<RemoveUnitChangeEventDto>(this, Id);
        dto.UnitId = UnitId;
        dto.Reason = Reason;
        return dto;
    }

    /// <summary>
    /// The country, plus the unit for as long as it is still standing in it.
    ///
    /// The unit drops out on its own once the removal has applied, which is exactly right for the two
    /// windows this feeds: a BLOCK window runs before Apply() and so points at the unit about to be
    /// hit, an AFTER-REACTION window runs after it, by which point
    /// <c>GameAPI.RemoveUnitFromCountry</c> has set CountryId to -1 and <c>CountryScene.RemoveUnit</c>
    /// has parked the scene off-board — its position would aim a camera at nothing. The inherited
    /// CountryId, captured at construction, survives both.
    /// </summary>
    public override TargetSet Targets() =>
        UnitState.IsDeployedToCountry
            ? base.Targets().Plus(TargetSet.Units(new[] { UnitId }))
            : base.Targets();

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

    public override void Mutate(BoardState board) => board.RemoveUnit(UnitId, Reason, TriggeringFaction);

    protected override Task OnLiveMutatedAsync()
    {
        GameAPI.PresentRemove(UnitId, CountryId);
        return Task.CompletedTask;
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