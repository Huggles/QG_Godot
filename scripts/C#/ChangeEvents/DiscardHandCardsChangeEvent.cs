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
        new ShowNotificationLabelAnimation(TriggeringFaction == TargetFaction
            ? $"{TargetFaction.WithPlayer()} discards {CardIds.Count} card(s)"
            : $"{TriggeringFaction.WithPlayer()} makes {TargetFaction.WithPlayer()} discard {CardIds.Count} card(s)", TriggeringFaction),
        // Self-triggered means the discard step, where the player picked these themselves; every
        // card-driven discard names a different triggering faction and the target picked nothing.
        new ShowDiscardModalAnimation(CardIds, "Discarded cards", TargetFaction, TriggeringFaction == TargetFaction)
    };

    public override void Mutate(BoardState board) => board.DiscardHandCards(TargetFaction, CardIds);

    protected override async Task OnLiveMutatedAsync()
    {
        GameAPI.PresentDiscardHand(TargetFaction, CardIds);
        await Task.Delay(GameSettings.DurationShort);
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