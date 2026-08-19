using System.Collections.Generic;
using System.Threading.Tasks;

public partial class DrawCardByNameChangeEvent : ChangeEvent
{
    public string CardName { get; set; }

    public DrawCardByNameChangeEvent(Faction triggeringFaction, Faction targetFaction, string cardName) : base(triggeringFaction)
    {
        TriggeringFaction = triggeringFaction;
        TargetFaction = targetFaction;
        CardName = cardName;
    }

    public override ChangeEventDto ToDto()
    {
        DrawCardByNameChangeEventDto dto = ChangeEventDto.Build<DrawCardByNameChangeEventDto>(this, Id);
        dto.CardName = CardName;
        return dto;
    }

    protected override async Task<bool> ExecuteAsync()
    {
        int cardId = DeckState.ForFaction(TargetFaction).DrawCardByName(CardName);
        if (cardId == -1)
            DebugUtilities.PrintPeerError($"DrawCardByNameChangeEvent: card '{CardName}' not found in {TargetFaction} deck");
        await Task.CompletedTask;
        return true;
    }

    /// <summary>
    /// CardName is a UniqueName, so show the player-facing Label where there is one. Falls back to
    /// the raw name rather than throwing — SummaryText() goes on the wire as
    /// InputRequest.TriggerSummaryText.
    /// </summary>
    public override string SummaryText() =>
        $"{TargetFaction.WithPlayer()} drew {StaticGameData.CardDataByName.GetValueOrDefault(CardName)?.Label ?? CardName} from their deck";
}
