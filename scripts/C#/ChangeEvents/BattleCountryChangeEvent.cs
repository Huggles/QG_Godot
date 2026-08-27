using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class BattleCountryChangeEvent : ChangeEvent
{
    public int CountryId { get; private set; }
    public CountryState CountryState => CountryState.ForId(CountryId);


    public BattleCountryChangeEvent(Faction triggeringFaction, int countryId) : base(triggeringFaction)
    {
        CountryId = countryId;
    }

    /// <summary>
    /// True when this event actually represents a battle.
    /// <para>
    /// RemoveUnitChangeEvent derives from this class, so a bare <c>is BattleCountryChangeEvent</c>
    /// test (or <c>GetChangeEvents&lt;BattleCountryChangeEvent&gt;()</c>) also matches eliminations
    /// and supply attrition. Always gate on this when you mean "a battle occurred" — otherwise
    /// cards like Frontal Assault trigger off a Rasputitsa elimination.
    /// </para>
    /// </summary>
    public bool IsBattle => this is not RemoveUnitChangeEvent removal || removal.Reason == UnitRemovalReason.BATTLE;

    /// <summary>
    /// The country the battle happened in. <see cref="RemoveUnitChangeEvent"/> widens this to the
    /// unit as well while there still is one.
    /// </summary>
    public override TargetSet Targets() => TargetSet.Countries(new[] { CountryId });

    protected override List<ChangeEventAnimation> BeforeAnimations => new()
    {
        new ZoomToCountryAnimation(CountryId),
    };

    protected override List<ChangeEventAnimation> AfterAnimations => new()
    {
        new ReturnCameraAnimation(),
    };

    public override ChangeEventDto ToDto()
    {
        BattleCountryChangeEventDto dto = ChangeEventDto.Build<BattleCountryChangeEventDto>(this, Id);
        dto.CountryId = CountryId;
        return dto;
    }

    protected async override Task<bool> ExecuteAsync()
    {
        await Task.CompletedTask;
        return true;
    }

    /// <summary>
    /// Must never throw: CardPlayRound puts SummaryText() on the wire as
    /// InputRequest.TriggerSummaryText, so a lookup that threw here would take down a turn rather
    /// than just garble a label. The subclasses RemoveUnitChangeEvent/BattleUnitChangeEvent
    /// override this with their own text.
    /// </summary>
    public override string SummaryText() =>
        $"{TriggeringFaction.WithPlayer()} battled in {CountryState?.Label ?? "an unknown country"}";
}