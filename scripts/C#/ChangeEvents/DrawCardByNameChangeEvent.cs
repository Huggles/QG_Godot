using System.Threading.Tasks;

public partial class DrawCardByNameChangeEvent : ChangeEvent
{
    public string CardName { get; set; }

    public DrawCardByNameChangeEvent(Faction triggeringFaction, Faction targetFaction, string cardName) : base(triggeringFaction)
    {
        TriggeringFaction = triggeringFaction;
        TargetFaction = targetFaction;
        CardName = cardName;
    }

    public override ChangeEventDto ToDto()
    {
        DrawCardByNameChangeEventDto dto = ChangeEventDto.Build<DrawCardByNameChangeEventDto>(this, Id);
        dto.CardName = CardName;
        return dto;
    }

    protected override async Task<bool> ExecuteAsync()
    {
        int cardId = DeckState.ForFaction(TargetFaction).DrawCardByName(CardName);
        if (cardId == -1)
            DebugUtilities.PrintPeerError($"DrawCardByNameChangeEvent: card '{CardName}' not found in {TargetFaction} deck");
        await Task.CompletedTask;
        return true;
    }
}
