using System.Collections.Generic;
using System.Threading.Tasks;

public enum RecycleDestination
{
    TopOfDeck,
    ShuffleIntoDeck,
    Hand
}

public partial class RecycleCardChangeEvent : ChangeEvent
{
    public int CardId { get; set; }
    public RecycleDestination Destination { get; set; }

    public RecycleCardChangeEvent(Faction triggeringFaction, Faction targetFaction, int cardId, RecycleDestination destination) : base(triggeringFaction)
    {
        TargetFaction = targetFaction;
        CardId = cardId;
        Destination = destination;
    }

    public override ChangeEventDto ToDto()
    {
        RecycleCardChangeEventDto dto = ChangeEventDto.Build<RecycleCardChangeEventDto>(this, Id);
        dto.CardId = CardId;
        dto.Destination = Destination;
        return dto;
    }

    protected override async Task<bool> ExecuteAsync()
    {
        DeckState deckState = DeckState.ForFaction(TargetFaction);
        deckState.DiscardedCardIds.Remove(CardId);
        switch (Destination)
        {
            case RecycleDestination.TopOfDeck:
                deckState.DeckCardIds.Insert(0, CardId);
                break;
            case RecycleDestination.ShuffleIntoDeck:
                deckState.DeckCardIds.Add(CardId);
                deckState.ShuffleDeck();
                break;
            case RecycleDestination.Hand:
                deckState.HandCardIds.Add(CardId);
                break;
        }
        await Task.CompletedTask;
        return true;
    }

    protected override List<ChangeEventAnimation> AfterAnimations => new()
    {
        new ShowNotificationLabelAnimation(SummaryText(), TriggeringFaction)
    };

    public override string SummaryText()
    {
        string dest = Destination switch
        {
            RecycleDestination.TopOfDeck => "top of draw deck",
            RecycleDestination.ShuffleIntoDeck => "draw deck (shuffled)",
            RecycleDestination.Hand => "hand",
            _ => "draw deck"
        };
        return $"{TriggeringFaction} recycled a card to {dest}";
    }
}
