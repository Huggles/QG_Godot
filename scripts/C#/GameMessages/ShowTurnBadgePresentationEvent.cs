using System.Collections.Generic;

/// <summary>
/// Announces the faction whose turn has just begun, as a badge in the middle of the screen.
///
/// Presentation only. The turn itself moved in <see cref="ChangeRoundChangeEvent"/>, which is what
/// every peer derives <c>GameFlow.CurrentFaction</c> from — this rides the same ordered channel
/// purely so the announcement lands with the turn change rather than somewhere alongside it.
/// </summary>
public partial class ShowTurnBadgePresentationEvent : PresentationEvent
{
    /// <param name="faction">The faction whose turn is starting — already current when this is sent.</param>
    public ShowTurnBadgePresentationEvent(Faction faction) : base(faction) { }

    /// <summary>
    /// The round change already writes its own history row, and this says the same thing about the
    /// same moment. A second entry per turn would push six rows a round of nothing into the log.
    /// </summary>
    public override bool ToHistoryItem => false;

    public override PresentationEventDto ToDto()
        => PresentationEventDto.Build<ShowTurnBadgePresentationEventDto>(this, Id);

    protected override List<ChangeEventAnimation> Animations => new()
    {
        new ShowTurnBadgeAnimation(TriggeringFaction)
    };

    public override string SummaryText() => $"{TriggeringFaction.Label()}'s turn";
}
