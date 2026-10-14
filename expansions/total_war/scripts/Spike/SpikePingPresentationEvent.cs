using System.Collections.Generic;

/// <summary>
/// M0a spike only: a replicated message defined by the expansion, registered at boot through
/// ExpansionRegistrar.AddMessage instead of a [JsonDerivedType] line on GameMessageDto. Shows a label,
/// like ShowActionLabelPresentationEvent. Delete with the spike.
/// </summary>
public partial class SpikePingPresentationEvent : PresentationEvent
{
    public string Text { get; set; }

    public SpikePingPresentationEvent(Faction triggeringFaction, string text) : base(triggeringFaction)
    {
        Text = text;
    }

    public override bool ToHistoryItem => false;

    public override PresentationEventDto ToDto()
    {
        SpikePingPresentationEventDto dto = PresentationEventDto.Build<SpikePingPresentationEventDto>(this, Id);
        dto.Text = Text;
        return dto;
    }

    protected override List<ChangeEventAnimation> Animations => new()
    {
        new ShowNotificationLabelAnimation(Text, TriggeringFaction)
    };

    public override string SummaryText() => Text;
}

public class SpikePingPresentationEventDto : PresentationEventDto
{
    public string Text { get; set; }
}
