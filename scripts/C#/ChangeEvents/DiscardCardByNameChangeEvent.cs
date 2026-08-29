using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Move a named card straight into its owner's discard pile.
///
/// The mirror image of <see cref="DrawCardByNameChangeEvent"/>, and it exists for the same reason:
/// scenario setup names cards, not ids, and the mutation has to ride the replicated stream so a
/// client reaches the same state by replaying it rather than by resolving the name itself.
///
/// Nothing in normal play discards a card by name — cards reach the pile through
/// DiscardHandCardsChangeEvent, ForceDiscardCardsChangeEvent or PlayCardChangeEvent — so this event
/// is deliberately animation-free: scenario setup places a pile, it does not narrate one.
/// </summary>
public partial class DiscardCardByNameChangeEvent : ChangeEvent
{
    public string CardName { get; set; }

    public DiscardCardByNameChangeEvent(Faction triggeringFaction, Faction targetFaction, string cardName) : base(triggeringFaction)
    {
        TriggeringFaction = triggeringFaction;
        TargetFaction = targetFaction;
        CardName = cardName;
    }

    public override ChangeEventDto ToDto()
    {
        DiscardCardByNameChangeEventDto dto = ChangeEventDto.Build<DiscardCardByNameChangeEventDto>(this, Id);
        dto.CardName = CardName;
        return dto;
    }

    protected override async Task<bool> ExecuteAsync()
    {
        int cardId = DeckState.ForFaction(TargetFaction).DiscardCardByName(CardName);
        if (cardId == -1)
        {
            DebugUtilities.PrintPeerError($"DiscardCardByNameChangeEvent: card '{CardName}' not found in {TargetFaction} deck");
            await Task.CompletedTask;
            return false;
        }

        // The discard pile is public, so a card that lands there is face up — otherwise a Response
        // card put in the pile by a scenario would render face down to everyone but its owner. Set
        // inside ExecuteAsync so both peers derive it from the same replayed message.
        CardState.ForId(cardId).IsRevealed = true;

        await Task.CompletedTask;
        return true;
    }

    /// <summary>
    /// CardName is a UniqueName, so show the player-facing Label where there is one. Falls back to
    /// the raw name rather than throwing — SummaryText() goes on the wire as
    /// InputRequest.TriggerSummaryText.
    /// </summary>
    public override string SummaryText() =>
        $"{TargetFaction.WithPlayer()} discarded {StaticGameData.CardDataByName.GetValueOrDefault(CardName)?.Label ?? CardName}";
}
