using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class DiscardHandCardsChangeEvent : ChangeEvent
{    
    public List<int> CardIds { get; set; }

    /// <summary>
    /// The turn's own Discard step discard (DiscardStepHandlerDefault), as opposed to a card's cost or
    /// effect. Replicated and saved, so the game's journal keeps it: Total War's Attrition reads it there.
    /// </summary>
    public bool IsDiscardStep { get; set; }

    /// <summary>
    /// Set for a cost of "discard a card of this type" (Total War Bolsters): the card stays face down, and
    /// other players are told only its type, so they can see the cost was paid. Replicated and saved.
    /// </summary>
    public CardType? RevealedType { get; set; }

    public DiscardHandCardsChangeEvent(Faction triggeringFaction, Faction targetFaction, List<int> cardIds) : base(triggeringFaction)
    {
        TriggeringFaction = triggeringFaction;
        TargetFaction = targetFaction;
        CardIds = cardIds;
    }

    public override ChangeEventDto ToDto()
    {
        DiscardHandCardsChangeEventDto dto = ChangeEventDto.Build<DiscardHandCardsChangeEventDto>(this, Id);
        dto.CardIds = CardIds;
        dto.IsDiscardStep = IsDiscardStep;
        dto.RevealedType = RevealedType;
        return dto;
    }

    protected override List<ChangeEventAnimation> AfterAnimations => new()
    {
        new ShowNotificationLabelAnimation(TriggeringFaction == TargetFaction
            ? $"{TargetFaction.WithPlayer()} discards {DiscardedText}"
            : $"{TriggeringFaction.WithPlayer()} makes {TargetFaction.WithPlayer()} discard {CardIds.Count} card(s)", TriggeringFaction),
        // Self-triggered means the discard step, where the player picked these themselves; every
        // card-driven discard names a different triggering faction and the target picked nothing.
        new ShowDiscardModalAnimation(CardIds, RevealedType is { } type ? $"Discarded {WithArticle(type)} card" : "Discarded cards", TargetFaction, TriggeringFaction == TargetFaction)
    };

    /// <summary>"a Response card" for a typed cost, else "N card(s)".</summary>
    private string DiscardedText => RevealedType is { } type ? $"{WithArticle(type)} card" : $"{CardIds.Count} card(s)";

    private static string WithArticle(CardType type) => ("AEIOU".Contains(type.Label()[0]) ? "an " : "a ") + type.Label();

    public override void Mutate(BoardState board) => board.DiscardHandCards(TargetFaction, CardIds);

    protected override async Task OnLiveMutatedAsync()
    {
        GameAPI.PresentDiscardHand(TargetFaction, CardIds);
        await Task.Delay(GameSettings.DurationShort);
    }

    /// <summary>
    /// Split on who is discarding: a self-discard is the common case and reads badly as
    /// "X made X discard". Null-coalesced because SummaryText() goes on the wire as
    /// InputRequest.TriggerSummaryText and must never throw.
    /// </summary>
    public override string SummaryText() =>
        TriggeringFaction == TargetFaction
            ? (RevealedType is { } type
                ? $"{TargetFaction.WithPlayer()} discarded {WithArticle(type)} card from hand"
                : $"{TargetFaction.WithPlayer()} discarded {CardIds?.Count ?? 0} hand card(s)")
            : $"{TriggeringFaction.WithPlayer()} made {TargetFaction.WithPlayer()} discard {CardIds?.Count ?? 0} hand card(s)";
}