using System.Collections.Generic;
using System.Threading.Tasks;

public partial class DrawCardsChangeEvent : ChangeEvent
{
    public int NumberOfCards { get; set; }
    public bool ShowDrawnCards { get; set; }

    public DrawCardsChangeEvent(Faction triggeringFaction, Faction targetFaction, int numberOfCards, bool showDrawnCards = true) : base(triggeringFaction)
    {
        TriggeringFaction = triggeringFaction;
        TargetFaction = targetFaction;
        NumberOfCards = numberOfCards;
        ShowDrawnCards = showDrawnCards;
    }

    public override ChangeEventDto ToDto()
    {
        DrawCardsChangeEventDto dto = ChangeEventDto.Build<DrawCardsChangeEventDto>(this, Id);
        dto.NumberOfCards = NumberOfCards;
        dto.ShowDrawnCards = ShowDrawnCards;
        return dto;
    }

    protected override async Task<bool> ExecuteAsync()
    {
        await GameAPI.DrawCards(TargetFaction, NumberOfCards, ShowDrawnCards);                
        return true;
    }
}
