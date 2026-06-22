using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class PlayCardChangeEvent : ChangeEvent
{
    public PlayCardChangeEvent(int cardId) : base(CardState.ForId(cardId).Faction)
    {
        SourceCardId = cardId;
    }

    public override ChangeEventDto ToDto()
    {
        PlayCardChangeEventDto dto = ChangeEventDto.Build<PlayCardChangeEventDto>(this, Id);
        dto.SourceCardId = SourceCardId;
        return dto;
    }

    protected override async Task<bool> ExecuteAsync(){
        DeckState.ForFaction(SourceCardState.Faction).PlayCard(SourceCardState.Id);
        
        if (!GameFlow.Instance.CardsPlayedThisTurnStep.ContainsKey(SourceCardState.Faction))
        {
            GameFlow.Instance.CardsPlayedThisTurnStep.Add(SourceCardState.Faction, 1);
        } 
        else
        {
            GameFlow.Instance.CardsPlayedThisTurnStep[SourceCardState.Faction] += 1;
        }
        
        await Task.CompletedTask;
        return true;
    }

    public override string SummaryText() => 
        SourceCardState.CardData.CardType == CardType.RESPONSE ? 
        $"{TriggeringFaction} played a response card (hidden)" :
        $"{TriggeringFaction} played {SourceCardState.CardData.CardType} card {SourceCardState.CardName}";
}
