using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Puts one of a faction's Air Forces on the board, in a space with its own supplied Army or Navy. Its
/// own event rather than a DeployUnitChangeEvent, which works out ARMY or NAVY from the destination and
/// carries no unit type on the wire.
/// </summary>
public partial class DeployAirForceChangeEvent : ChangeEvent
{
    /// <summary>The deployed Air Force, or -1 until <see cref="Mutate"/> has placed it. See DeployUnitChangeEvent.UnitId.</summary>
    public int UnitId { get; set; } = -1;
    public int CountryId { get; set; }

    public CountryState CountryState => CountryState.ForId(CountryId);

    public DeployAirForceChangeEvent(Faction triggeringFaction, int countryId) : base(triggeringFaction)
    {
        CountryId = countryId;
    }

    public override ChangeEventDto ToDto()
    {
        DeployAirForceChangeEventDto dto = ChangeEventDto.Build<DeployAirForceChangeEventDto>(this, Id);
        dto.CountryId = CountryId;
        return dto;
    }

    public override TargetSet Targets() =>
        UnitId > -1
            ? TargetSet.Countries(new[] { CountryId }).Plus(TargetSet.Units(new[] { UnitId }))
            : TargetSet.Countries(new[] { CountryId });

    protected override List<ChangeEventAnimation> BeforeAnimations => new() { new ZoomToCountryAnimation(CountryId) };
    protected override List<ChangeEventAnimation> AfterAnimations => new() { new ReturnCameraAnimation() };

    public override void Mutate(BoardState board) => UnitId = board.DeployAirForce(TriggeringFaction, CountryId);

    protected override Task OnLiveMutatedAsync()
    {
        GameAPI.PresentDeploy(UnitId, CountryId, BlockAnimationQueue);
        return Task.CompletedTask;
    }

    /// <summary>Never throws: it goes on the wire as InputRequest.TriggerSummaryText. See DeployUnitChangeEvent.SummaryText.</summary>
    public override string SummaryText()
    {
        CountryState country = GameSession.Current != null ? CountryState.ForId(CountryId) : null;
        return $"{TriggeringFaction.WithPlayer()} deployed an Air Force in {country?.Label ?? $"country {CountryId}"}";
    }
}
