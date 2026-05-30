using System.Threading.Tasks;

public partial class DrawCardsChangeEvent : ChangeEvent
{
    public int NumberOfCards { get; set; }
    public Faction TargetFaction { get; set; }
    FactionState triggeringFactionState => FactionState.ForEnum(TriggeringFaction);
    FactionState targetFactionState => FactionState.ForEnum(TargetFaction);

    public DrawCardsChangeEvent(Faction triggeringFaction, Faction targetFaction, int numberOfCards) : base(triggeringFaction)
    {
        TriggeringFaction = triggeringFaction;
        TargetFaction = targetFaction;
        NumberOfCards = numberOfCards;
    }

    public override ChangeEventDto ToDto() => new DrawCardsChangeEventDto
    {
        TriggeringFaction = TriggeringFaction, SourceCardId = SourceCardId,
        IsTrigger = IsTrigger, SuppressGameProgress = SuppressGameProgress,
        TargetFaction = TargetFaction, NumberOfCards = NumberOfCards
    };

    protected override async Task<bool> ExecuteAsync()
    {
        DeckState deckState = DeckState.ForFaction(TargetFaction);
        deckState.DrawCards(NumberOfCards);
        PlayerActionLabel.ShowText($"{triggeringFactionState.FactionData.Label} gives {targetFactionState.FactionData.Label} {NumberOfCards} card(s)", TriggeringFaction);
        await Task.Delay(GameSettings.PauseDuration);
        return true;
    }
}
