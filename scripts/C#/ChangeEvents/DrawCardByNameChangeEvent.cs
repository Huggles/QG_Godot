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

    /// <summary>Filled by Mutate for the history popup; -1 until then, or if the card was not found.</summary>
    public int DrawnCardId { get; private set; } = -1;

    public override void Mutate(BoardState board)
    {
        int cardId = DrawnCardId = board.DrawCardByName(TargetFaction, CardName);
        if (cardId == -1 && board.IsLive)
            DebugUtilities.PrintPeerError($"DrawCardByNameChangeEvent: card '{CardName}' not found in {TargetFaction} deck");
    }

    /// <summary>
    /// CardName is a UniqueName, so show the player-facing Label where there is one. Falls back to
    /// the raw name rather than throwing — SummaryText() goes on the wire as
    /// InputRequest.TriggerSummaryText.
    /// </summary>
    public override string SummaryText() =>
        $"{TargetFaction.WithPlayer()} drew {StaticGameData.CardDataByName.GetValueOrDefault(CardName)?.Label ?? CardName} from their deck";
}
