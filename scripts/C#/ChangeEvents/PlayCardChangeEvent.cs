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
    ///
    /// The title is redacted for a face-down Response card, matching what CardFace draws for it and the
    /// split SummaryText() already makes below. Animations are never serialized — each peer builds its
    /// own from this factory — so a per-peer title is safe.
    /// </summary>
    protected override List<ChangeEventAnimation> AfterAnimations => new()
    {
        new ShowCardsModalAnimation(
            new List<int> { SourceCardId },
            SourceCardState.IsFaceVisibleToLocalPlayer
                ? $"{TriggeringFaction.WithPlayer()} plays {SourceCardState.CardName}"
                : $"{TriggeringFaction.WithPlayer()} plays a Response card")
    };

    public override void Mutate(BoardState board)
    {
        board.PlayCard(SourceCardState.Faction, SourceCardState.Id);
        board.CountPlay(SourceCardState.Faction);

        CardRecord card = board.Of(SourceCardState);
        card.PlayedInTurn.Add(board.GameTurn);
        card.IsRevealed = SourceCardState.CardData.CardType != CardType.RESPONSE;
    }

    /// <summary>
    /// Decks because DeckState.PlayCard moves the card out of hand and into the pool or discard; Flow
    /// because it bumps CardsPlayedThisTurnStep and stamps PlayedInTurn, both of which card conditions
    /// read. Whatever the card then DOES arrives as its own nested ChangeEvents, each carrying its own
    /// scope — a card that deploys a unit raises a DeployUnitChangeEvent, and that one is Board.
    /// A Status card also registers its modifier, which can change supply itself (Scorched Earth).
    /// </summary>
    public override RecalcScope RecalcScope => SourceCardState.CardData.CardType == CardType.STATUS
        ? RecalcScope.All
        : RecalcScope.Decks | RecalcScope.Flow;

    public override string SummaryText() =>
        SourceCardState.CardData.CardType == CardType.RESPONSE ? 
        $"{TriggeringFaction.WithPlayer()} played a response card (hidden)" :
        $"{TriggeringFaction.WithPlayer()} played {SourceCardState.CardData.CardType.Label()} card {SourceCardState.CardName}";
}
