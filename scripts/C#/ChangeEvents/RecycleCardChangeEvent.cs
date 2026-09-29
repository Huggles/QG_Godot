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
    /// Needed because Mutate runs on both peers and the state hash covers deck *counts* but not
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
        // Safe to read here: BroadCast (and so ToDto) runs after Mutate has set it.
        dto.ShuffledOrder = ShuffledOrder;
        return dto;
    }

    protected override void ApplyDtoFields(GameMessageDto dto)
    {
        base.ApplyDtoFields(dto);
        if (dto is RecycleCardChangeEventDto d) ShuffledOrder = d.ShuffledOrder;
    }

    /// <summary>Mirror of Mutate: the card leaves whatever pile it is in for the hand or the deck.</summary>
    public override void Project(BoardProjection projection)
    {
        if (projection.PileOf(CardId) == CardPile.None) { projection.MarkUnknown($"recycle card {CardId}, which is in no pile"); return; }
        projection.MoveCard(CardId, Destination == RecycleDestination.Hand ? CardPile.Hand : CardPile.Deck);
    }

    /// <summary>
    /// The host decides a shuffle before anything moves: the deck as it will be once the card has
    /// joined it, shuffled with the same draws the in-place shuffle used to make. A client and a replay
    /// take the recorded order instead, and so does every board this event mutates.
    /// </summary>
    protected override Task ResolveChoicesAsync()
    {
        if (Destination != RecycleDestination.ShuffleIntoDeck || !IsServer || ReplayContext.IsReplaying)
            return Task.CompletedTask;

        FactionRecord piles = BoardState.Live.ForFaction(TargetFaction);
        bool inAnyPile = piles.Hand.Contains(CardId) || piles.Deck.Contains(CardId) || piles.Discarded.Contains(CardId)
                      || piles.Response.Contains(CardId) || piles.Status.Contains(CardId);
        if (!inAnyPile) return Task.CompletedTask;

        List<int> order = new(piles.Deck);
        order.Remove(CardId);
        order.Add(CardId);
        GameRandom.Shuffle(order);
        ShuffledOrder = order;
        return Task.CompletedTask;
    }

    // Whether the last mutation found the card to move; a card in no pile changes nothing at all.
    private bool _moved;

    public override void Mutate(BoardState board)
    {
        FactionRecord piles = board.ForFaction(TargetFaction);

        // Recycling MOVES the card, so it has to leave wherever it currently is — adding it to the
        // destination while the original stayed put would make it two cards. See
        // BoardState.RemoveCardFromAnyPile.
        _moved = board.RemoveCardFromAnyPile(TargetFaction, CardId);
        if (!_moved)
        {
            // Not in any of this faction's piles: adding it to the destination would conjure a card
            // that was never theirs. Both peers see the same state here, so both skip identically.
            if (board.IsLive) DebugUtilities.PrintPeerError($"Cannot recycle card {CardId}: not in any pile of {TargetFaction}");
            return;
        }

        switch (Destination)
        {
            case RecycleDestination.TopOfDeck:
                piles.Deck.Insert(0, CardId);
                break;
            case RecycleDestination.ShuffleIntoDeck:
                piles.Deck.Add(CardId);
                // The order was decided in ResolveChoicesAsync, or arrived with the event. Replay
                // re-applies recorded outcomes rather than re-deciding them, and ComputeHash covers deck
                // counts, not order, so a fresh shuffle would diverge silently. A projection that was
                // never resolved keeps the deck unshuffled: a fork does not draw.
                if (ShuffledOrder != null) board.ShuffleDeck(TargetFaction, ShuffledOrder);
                break;
            case RecycleDestination.Hand:
                piles.Hand.Add(CardId);
                break;
            case RecycleDestination.BottomOfDeck:
                piles.Deck.Add(CardId);
                break;
        }

        // Back in play means face down again: a Response card revealed by an earlier activation must
        // not stay revealed once it returns to a deck or a hand.
        board.ForCard(CardId).IsRevealed = false;
    }

    /// <summary>
    /// ...and unused again. A card whose steps are still marked finished is drawn as a dead card: no
    /// executable steps, so it is never playable again. See CardLogic.OnReturnedToPlay. Step progress
    /// lives on the card, not the board, so this is the live half. Runs on both peers.
    /// </summary>
    protected override Task OnLiveMutatedAsync()
    {
        if (_moved) CardState.ForId(CardId).CardLogic?.OnReturnedToPlay();
        return Task.CompletedTask;
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
