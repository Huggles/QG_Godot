using System.Collections.Generic;

/// <summary>
/// A Bolster card used straight from hand: the introduction event CardPlayRound.DoCard applies in place of
/// PlayCardChangeEvent, then the card's steps run. It moves the card to the discard pile ("then discarded"),
/// turns it face up and counts as its activation this turn, and never calls board.CountPlay, so it does not
/// spend the turn's play. Shown once: a later step of the same card is resumed from the discard pile, which
/// DoCard introduces with ActivateReactionChangeEvent (first step only), so "X uses Y" cannot repeat.
///
/// Like every introduction it is not a trigger and opens no window of its own.
/// </summary>
public partial class UseBolsterChangeEvent : ChangeEvent
{
    public UseBolsterChangeEvent(int cardId) : base(CardState.ForId(cardId).Faction)
    {
        SourceCardId = cardId;
        IsTrigger = false;
    }

    public override ChangeEventDto ToDto()
    {
        UseBolsterChangeEventDto dto = ChangeEventDto.Build<UseBolsterChangeEventDto>(this, Id);
        dto.SourceCardId = SourceCardId;
        return dto;
    }

    protected override List<ChangeEventAnimation> AfterAnimations => new()
    {
        new ShowCardAnnouncementAnimation(
            SourceCardId,
            $"{TriggeringFaction.WithPlayer()} uses {SourceCardState.CardName}")
    };

    public override void Mutate(BoardState board)
    {
        CardRecord card = board.Of(SourceCardState);
        card.ActivatedInTurns.Add(board.GameTurn);
        card.IsRevealed = true;
        board.DiscardCard(TriggeringFaction, SourceCardId);
    }

    /// <summary>Decks: the card leaves the hand for the discard pile. Flow: its activation this turn, which card conditions read.</summary>
    public override RecalcScope RecalcScope => RecalcScope.Decks | RecalcScope.Flow;

    public override string SummaryText() => $"{TriggeringFaction.WithPlayer()} used Bolster card {SourceCardState.CardName}";
}
