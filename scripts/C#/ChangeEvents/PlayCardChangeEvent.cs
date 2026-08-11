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

    /// <summary>
    /// Announce the played card to every player. Enqueued from Apply(), which ProcessIntroductionEvent
    /// runs before block reactions are requested — so the card is shown as it hits the table, even if a
    /// block cancels it a moment later.
    /// </summary>
    protected override List<ChangeEventAnimation> AfterAnimations => new()
    {
        new ShowCardsModalAnimation(
            new List<int> { SourceCardId },
            $"{FactionState.ForEnum(TriggeringFaction).FactionData.Label} plays {SourceCardState.CardName}")
    };

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

        SourceCardState.PlayedInTurn.Add(GameFlow.Instance.GameTurn);
        
        await Task.CompletedTask;
        return true;
    }

    public override string SummaryText() => 
        SourceCardState.CardData.CardType == CardType.RESPONSE ? 
        $"{TriggeringFaction} played a response card (hidden)" :
        $"{TriggeringFaction} played {SourceCardState.CardData.CardType} card {SourceCardState.CardName}";
}
