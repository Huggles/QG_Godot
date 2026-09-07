using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Asks the target faction to select and discard <see cref="NumberOfCards"/> cards from their hand.
/// Uses the existing hand-discard UI (same as the end-of-turn discard step).
/// </summary>
public partial class ForceDiscardHandCardsChangeEvent : ChangeEvent
{
    public int NumberOfCards { get; set; }
    public List<int> DiscardedCardIds { get; private set; } = new();

    /// <summary>
    /// True once the selection is known, and the guard on raising the input request.
    ///
    /// ExecuteAsync runs on every peer, but only the host may raise an input request — so a replaying
    /// client must be *told* what was picked rather than ask again. The ids round-trip via
    /// ToDto/ApplyDtoFields, the same shape as RecycleCardChangeEvent.ShuffledOrder and
    /// ForceDiscardCardsChangeEvent.ModifiersApplied. A separate flag rather than
    /// <c>DiscardedCardIds.Count > 0</c> so that an empty-but-resolved selection still counts as
    /// answered.
    /// </summary>
    public bool SelectionResolved { get; private set; }

    /// <summary>
    /// Pay the discard out of the front of the offered cards when the request is aborted without an
    /// answer, instead of letting <see cref="StepSkippedException"/> abandon whatever raised it.
    ///
    /// Off by default, because for a card's own cost an abandoned step is the right outcome — the
    /// player does not pay and the card does not resolve. It is opt-in for the callers where the
    /// discard is a debt the game flow has already committed to and cannot leave unpaid, such as
    /// CardPlayRound's penalty for ending a play step without taking the play action.
    ///
    /// Host-side only, like the request itself: a client is told the selection through the DTO.
    /// </summary>
    public bool AutoDiscardOnAbort { get; set; }

    public ForceDiscardHandCardsChangeEvent(Faction triggeringFaction, Faction targetFaction, int numberOfCards) : base(triggeringFaction)
    {
        TriggeringFaction = triggeringFaction;
        TargetFaction = targetFaction;
        NumberOfCards = numberOfCards;
    }

    /// <summary>
    /// Supply the selection up front, for a caller that has already asked the player. The opening
    /// discard asks every player at the same time and then applies the events one by one, so its
    /// requests cannot live inside ExecuteAsync — concurrent Apply() calls would interleave their
    /// broadcasts and state hashes.
    /// </summary>
    public void PreselectDiscards(List<int> cardIds)
    {
        DiscardedCardIds = cardIds ?? new List<int>();
        SelectionResolved = true;
    }

    public override ChangeEventDto ToDto()
    {
        ForceDiscardHandCardsChangeEventDto dto = ChangeEventDto.Build<ForceDiscardHandCardsChangeEventDto>(this, Id);
        dto.NumberOfCards = NumberOfCards;
        // Safe to read here: BroadCast (and so ToDto) runs after ExecuteAsync has resolved it.
        dto.DiscardedCardIds = DiscardedCardIds;
        return dto;
    }

    protected override void ApplyDtoFields(GameMessageDto dto)
    {
        base.ApplyDtoFields(dto);
        if (dto is ForceDiscardHandCardsChangeEventDto d) PreselectDiscards(d.DiscardedCardIds);
    }

    protected override async Task<bool> ExecuteAsync()
    {
        if (!SelectionResolved)
        {
            // Held in a local so the offered set survives an abort: SendInputRequest calls
            // PopulateTargets before anything reaches the wire, so TargetCardIds is filled on this
            // instance even on the paths that throw.
            InputRequest.ForceDiscardHandCardsRequestHandler request = new(TargetFaction, NumberOfCards);

            List<int> selection;
            try
            {
                selection = (await request.BroadCast()).ResponseCardIds;
            }
            catch (StepSkippedException) when (AutoDiscardOnAbort)
            {
                DebugUtilities.PrintPeer($"Forced discard for {TargetFaction} was released without an answer — paying it from the offered cards");
                selection = new List<int>();
            }

            // Covers a short answer as well as a released one: whatever is still owed comes off the
            // front of the offered cards. Gated on the same flag, so a caller that has not opted in
            // keeps answering with exactly what came back.
            if (AutoDiscardOnAbort && selection.Count < NumberOfCards)
            {
                selection = selection
                    .Union(request.TargetCardIds ?? DeckState.ForFaction(TargetFaction).HandCardIds)
                    .Take(NumberOfCards)
                    .ToList();
            }

            PreselectDiscards(selection);
        }
        await GameAPI.DiscardHandCards(TargetFaction, DiscardedCardIds);
        return true;
    }

    protected override List<ChangeEventAnimation> AfterAnimations => new()
    {
        new ShowNotificationLabelAnimation($"{TargetFaction.WithPlayer()} discards {NumberOfCards} hand card(s)", TriggeringFaction),
        new ShowDiscardModalAnimation(DiscardedCardIds, "Discarded cards", TargetFaction)
    };

    public override string SummaryText() => $"{TargetFaction.WithPlayer()} discarded {DiscardedCardIds.Count} hand card(s)";
}
