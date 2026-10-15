using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Air Superiority used on a space with no enemy Air Force: nothing is removed, but it can be reacted to,
/// which is why the rules allow it. On an enemy Air Force the action is a RemoveUnitChangeEvent with reason
/// AIR_SUPERIORITY instead, as a battle on a unit is a BattleUnitChangeEvent. Not a battle.
/// </summary>
public partial class AirSuperiorityChangeEvent : ChangeEvent
{
    public int CountryId { get; private set; }

    public AirSuperiorityChangeEvent(Faction triggeringFaction, int countryId) : base(triggeringFaction)
    {
        CountryId = countryId;
    }

    public override ChangeEventDto ToDto()
    {
        AirSuperiorityChangeEventDto dto = ChangeEventDto.Build<AirSuperiorityChangeEventDto>(this, Id);
        dto.CountryId = CountryId;
        return dto;
    }

    public override TargetSet Targets() => TargetSet.Countries(new[] { CountryId });

    protected override List<ChangeEventAnimation> BeforeAnimations => new() { new ZoomToCountryAnimation(CountryId) };
    protected override List<ChangeEventAnimation> AfterAnimations => new() { new ReturnCameraAnimation() };

    public override void Mutate(BoardState board) { }

    /// <summary>Never throws: it goes on the wire as InputRequest.TriggerSummaryText.</summary>
    public override string SummaryText()
    {
        CountryState country = GameSession.Current != null ? CountryState.ForId(CountryId) : null;
        return $"{TriggeringFaction.WithPlayer()} used Air Superiority on {country?.Label ?? $"country {CountryId}"}";
    }
}
