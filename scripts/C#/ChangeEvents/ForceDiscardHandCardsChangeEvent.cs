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

    public ForceDiscardHandCardsChangeEvent(Faction triggeringFaction, Faction targetFaction, int numberOfCards) : base(triggeringFaction)
    {
        TriggeringFaction = triggeringFaction;
        TargetFaction = targetFaction;
        NumberOfCards = numberOfCards;
    }

    public override ChangeEventDto ToDto()
    {
        ForceDiscardHandCardsChangeEventDto dto = ChangeEventDto.Build<ForceDiscardHandCardsChangeEventDto>(this, Id);
        dto.NumberOfCards = NumberOfCards;
        return dto;
    }

    protected override async Task<bool> ExecuteAsync()
    {
        InputRequest response = await new InputRequest.ForceDiscardHandCardsRequestHandler(TargetFaction, NumberOfCards).BroadCast();
        DiscardedCardIds = response.ResponseCardIds;
        await GameAPI.DiscardHandCards(TargetFaction, DiscardedCardIds);
        return true;
    }

    protected override List<ChangeEventAnimation> AfterAnimations => new()
    {
        new ShowNotificationLabelAnimation($"{TargetFaction} discards {NumberOfCards} hand card(s)", TriggeringFaction),
        new ShowDiscardModalAnimation(DiscardedCardIds, "Discarded cards")
    };

    public override string SummaryText() => $"{TargetFaction} discarded {DiscardedCardIds.Count} hand card(s)";
}
