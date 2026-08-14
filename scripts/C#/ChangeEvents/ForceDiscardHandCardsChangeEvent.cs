using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Asks the target faction to select and discard <see cref="NumberOfCards"/> cards from their hand.
/// Uses the existing hand-discard UI (same as the end-of-turn discard step).
/// </summary>
public partial class ForceDiscardHandCardsChangeEvent : ChangeEvent
{
    public int NumberOfCards { get; set; }
    public List<int> DiscardedCardIds { get; private set; } = new();

    /// <summary>
    /// True once the selection is known, and the guard on raising the input request.
    ///
    /// ExecuteAsync runs on every peer, but only the host may raise an input request — so a replaying
    /// client must be *told* what was picked rather than ask again. The ids round-trip via
    /// ToDto/ApplyDtoFields, the same shape as RecycleCardChangeEvent.ShuffledOrder and
    /// ForceDiscardCardsChangeEvent.ModifiersApplied. A separate flag rather than
    /// <c>DiscardedCardIds.Count > 0</c> so that an empty-but-resolved selection still counts as
    /// answered.
    /// </summary>
    public bool SelectionResolved { get; private set; }

    public ForceDiscardHandCardsChangeEvent(Faction triggeringFaction, Faction targetFaction, int numberOfCards) : base(triggeringFaction)
    {
        TriggeringFaction = triggeringFaction;
        TargetFaction = targetFaction;
        NumberOfCards = numberOfCards;
    }

    /// <summary>
    /// Supply the selection up front, for a caller that has already asked the player. The opening
    /// discard asks every player at the same time and then applies the events one by one, so its
    /// requests cannot live inside ExecuteAsync — concurrent Apply() calls would interleave their
    /// broadcasts and state hashes.
    /// </summary>
    public void PreselectDiscards(List<int> cardIds)
    {
        DiscardedCardIds = cardIds ?? new List<int>();
        SelectionResolved = true;
    }

    public override ChangeEventDto ToDto()
    {
        ForceDiscardHandCardsChangeEventDto dto = ChangeEventDto.Build<ForceDiscardHandCardsChangeEventDto>(this, Id);
        dto.NumberOfCards = NumberOfCards;
        // Safe to read here: BroadCast (and so ToDto) runs after ExecuteAsync has resolved it.
        dto.DiscardedCardIds = DiscardedCardIds;
        return dto;
    }

    protected override void ApplyDtoFields(GameMessageDto dto)
    {
        base.ApplyDtoFields(dto);
        if (dto is ForceDiscardHandCardsChangeEventDto d) PreselectDiscards(d.DiscardedCardIds);
    }

    protected override async Task<bool> ExecuteAsync()
    {
        if (!SelectionResolved)
        {
            InputRequest response = await new InputRequest.ForceDiscardHandCardsRequestHandler(TargetFaction, NumberOfCards).BroadCast();
            PreselectDiscards(response.ResponseCardIds);
        }
        await GameAPI.DiscardHandCards(TargetFaction, DiscardedCardIds);
        return true;
    }

    protected override List<ChangeEventAnimation> AfterAnimations => new()
    {
        new ShowNotificationLabelAnimation($"{TargetFaction.WithPlayer()} discards {NumberOfCards} hand card(s)", TriggeringFaction),
        new ShowDiscardModalAnimation(DiscardedCardIds, "Discarded cards", TargetFaction)
    };

    public override string SummaryText() => $"{TargetFaction.WithPlayer()} discarded {DiscardedCardIds.Count} hand card(s)";
}
