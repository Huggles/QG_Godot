using System.Collections.Generic;
using System.Threading.Tasks;

public partial class ReorderDeckChangeEvent : ChangeEvent
{
    public List<int> ReorderedCardIds { get; set; }
 
    public ReorderDeckChangeEvent(Faction triggeringFaction, List<int> reorderedCardIds) : base(triggeringFaction)
    {
        TargetFaction = triggeringFaction;
        ReorderedCardIds = reorderedCardIds;
    }

    public override ChangeEventDto ToDto()
    {
        ReorderDeckChangeEventDto dto = ChangeEventDto.Build<ReorderDeckChangeEventDto>(this, Id);
        dto.ReorderedCardIds = ReorderedCardIds;
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

    public override string SummaryText() => $"{TargetFaction} reordered the top {ReorderedCardIds.Count} cards of their deck";
}
