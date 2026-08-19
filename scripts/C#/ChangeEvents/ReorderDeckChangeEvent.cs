using System.Collections.Generic;
using System.Threading.Tasks;

public partial class ReorderDeckChangeEvent : ChangeEvent
{
    /// <summary>
    /// The deck's complete new order. ExecuteAsync replaces DeckCardIds outright, so a caller that
    /// only rearranged the top few cards still has to pass the untouched remainder after them — see
    /// StatusSuperiorPlanning. That is why the count here says nothing about how much actually moved,
    /// and why <see cref="ReorderedFromTop"/> exists.
    /// </summary>
    public List<int> ReorderedCardIds { get; set; }

    /// <summary>
    /// How many cards from the top the caller actually rearranged, or -1 for a whole-deck shuffle.
    ///
    /// Replicated, because it is display-only but the display is per-peer: every peer replays this
    /// event through FromDto and builds its own history row from its own SummaryText(). A host-only
    /// field would leave clients describing the opening shuffle as a four-card peek.
    /// </summary>
    public int ReorderedFromTop { get; set; } = -1;

    /// <summary>True when the whole deck was shuffled rather than a few cards rearranged.</summary>
    public bool IsFullShuffle => ReorderedFromTop < 0;

    public ReorderDeckChangeEvent(Faction triggeringFaction, List<int> reorderedCardIds) : base(triggeringFaction)
    {
        TargetFaction = triggeringFaction;
        ReorderedCardIds = reorderedCardIds;
    }

    public override ChangeEventDto ToDto()
    {
        ReorderDeckChangeEventDto dto = ChangeEventDto.Build<ReorderDeckChangeEventDto>(this, Id);
        dto.ReorderedCardIds = ReorderedCardIds;
        dto.ReorderedFromTop = ReorderedFromTop;
        return dto;
    }

    protected override async Task<bool> ExecuteAsync()
    {
        DeckState deck = DeckState.ForFaction(TargetFaction);
        deck.DeckCardIds.Clear();
        deck.DeckCardIds.AddRange(ReorderedCardIds);
        await Task.CompletedTask;
        return true;
    }

    /// <summary>
    /// Two genuinely different events share this type, and the payload alone cannot tell them apart —
    /// both carry the full deck. The old text read the payload count as a reorder depth, so the
    /// opening shuffle announced itself as "reordered the top 46 cards".
    /// </summary>
    public override string SummaryText() => IsFullShuffle
        ? $"{TargetFaction.WithPlayer()} shuffled their draw deck ({ReorderedCardIds?.Count ?? 0} cards)"
        : $"{TargetFaction.WithPlayer()} reordered the top {ReorderedFromTop} card(s) of their draw deck";
}
