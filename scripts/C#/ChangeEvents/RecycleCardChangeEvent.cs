using System.Collections.Generic;
using System.Threading.Tasks;

public enum RecycleDestination
{
    TopOfDeck,
    ShuffleIntoDeck,
    Hand,
    BottomOfDeck
}

public partial class RecycleCardChangeEvent : ChangeEvent
{
    public int CardId { get; set; }
    public RecycleDestination Destination { get; set; }

    /// <summary>
    /// The deck order the host produced for a <see cref="RecycleDestination.ShuffleIntoDeck"/>, and
    /// the client's instruction to match it. Null for every other destination.
    ///
    /// Needed because ExecuteAsync runs on both peers and the state hash covers deck *counts* but not
    /// order, so two independent shuffles would diverge undetected. It is not a constructor parameter,
    /// so it round-trips via ToDto/ApplyDtoFields — the same shape as
    /// ForceDiscardCardsChangeEvent.ModifiersApplied, and for the same reason: a field the client must
    /// be told rather than recompute.
    /// </summary>
    public List<int> ShuffledOrder { get; set; }

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
        // Safe to read here: BroadCast (and so ToDto) runs after ExecuteAsync has set it.
        dto.ShuffledOrder = ShuffledOrder;
        return dto;
    }

    protected override void ApplyDtoFields(GameMessageDto dto)
    {
        base.ApplyDtoFields(dto);
        if (dto is RecycleCardChangeEventDto d) ShuffledOrder = d.ShuffledOrder;
    }

    protected override async Task<bool> ExecuteAsync()
    {
        DeckState deckState = DeckState.ForFaction(TargetFaction);

        // Recycling MOVES the card, so it has to leave wherever it currently is. This used to be
        // `DiscardedCardIds.Remove(CardId)` — right for every in-game caller, all of which recycle out
        // of the discard pile, but a silent duplication for any other source: the card was added to the
        // destination while the original copy stayed put, and it then existed in two piles at once
        // (hand *and* draw deck, say). See DeckState.RemoveCardFromAnyPile for why that reads as two
        // separate cards to the rest of the game.
        if (!deckState.RemoveCardFromAnyPile(CardId))
        {
            // Not in any of this faction's piles: adding it to the destination would conjure a card
            // that was never theirs. Both peers see the same state here, so both skip identically.
            DebugUtilities.PrintPeerError($"Cannot recycle card {CardId}: not in any pile of {TargetFaction}");
            return false;
        }

        switch (Destination)
        {
            case RecycleDestination.TopOfDeck:
                deckState.DeckCardIds.Insert(0, CardId);
                break;
            case RecycleDestination.ShuffleIntoDeck:
                deckState.DeckCardIds.Add(CardId);
                // Host shuffles and records the order; the client replays that exact order rather
                // than shuffling for itself. See DeckState.ShuffleDeck.
                //
                // ...and so does the host while restoring a save. Replay re-applies recorded outcomes; it does
                // not re-decide them. Shuffling afresh here would silently give the restored game a different
                // deck order from the saved one, and MultiplayerGameState.ComputeHash covers deck COUNTS, not
                // order, so nothing downstream would catch it.
                ShuffledOrder = deckState.ShuffleDeck(IsServer && !ReplayContext.IsReplaying ? null : ShuffledOrder);
                break;
            case RecycleDestination.Hand:
                deckState.HandCardIds.Add(CardId);
                break;
            case RecycleDestination.BottomOfDeck:
                deckState.DeckCardIds.Add(CardId);
                break;
        }

        // Back in play means face down again: a Response card revealed by an earlier activation must
        // not stay revealed once it returns to a deck or a hand. Placed after the RemoveCardFromAnyPile
        // guard above, so a recycle that found nothing to move changes nothing here either.
        CardState.ForId(CardId).IsRevealed = false;

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
            RecycleDestination.BottomOfDeck => "bottom of draw deck",
            _ => "draw deck"
        };
        return $"{TriggeringFaction.WithPlayer()} recycled a card to {dest}";
    }
}
