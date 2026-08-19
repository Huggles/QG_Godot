using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class DiscardHandCardsChangeEvent : ChangeEvent
{    
    public List<int> CardIds { get; set; }

    public DiscardHandCardsChangeEvent(Faction triggeringFaction, Faction targetFaction, List<int> cardIds) : base(triggeringFaction)
    {
        TriggeringFaction = triggeringFaction;
        TargetFaction = targetFaction;
        CardIds = cardIds;
    }

    public override ChangeEventDto ToDto()
    {
        DiscardHandCardsChangeEventDto dto = ChangeEventDto.Build<DiscardHandCardsChangeEventDto>(this, Id);
        dto.CardIds = CardIds;
        return dto;
    }

    protected override List<ChangeEventAnimation> AfterAnimations => new()
    {
        new ShowNotificationLabelAnimation($"{TriggeringFaction.WithPlayer()} makes {TargetFaction.WithPlayer()} discard {CardIds.Count} cards", TriggeringFaction),
        new ShowDiscardModalAnimation(CardIds, "Discarded cards", TargetFaction)
    };

    protected override async Task<bool> ExecuteAsync()
    {
        await GameAPI.DiscardHandCards(TargetFaction, CardIds);
        await Task.Delay(GameSettings.DurationShort);
        return true;
    }

    /// <summary>
    /// Split on who is discarding: a self-discard is the common case and reads badly as
    /// "X made X discard". Null-coalesced because SummaryText() goes on the wire as
    /// InputRequest.TriggerSummaryText and must never throw.
    /// </summary>
    public override string SummaryText() =>
        TriggeringFaction == TargetFaction
            ? $"{TargetFaction.WithPlayer()} discarded {CardIds?.Count ?? 0} hand card(s)"
            : $"{TriggeringFaction.WithPlayer()} made {TargetFaction.WithPlayer()} discard {CardIds?.Count ?? 0} hand card(s)";
}