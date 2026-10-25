using System.Collections.Generic;

/// <summary>
/// An Air Power card spent from hand on an Air step action (Deploy, Air Superiority): turned face up, discarded
/// and shown to everyone as "X uses Y", as a played card is. Not a trigger, like the discard it replaces;
/// the action's own event that follows is what can be reacted to.
/// </summary>
public partial class UseAirPowerChangeEvent : ChangeEvent
{
    public UseAirPowerChangeEvent(int cardId) : base(CardState.ForId(cardId).Faction)
    {
        SourceCardId = cardId;
        IsTrigger = false;
    }

    public override ChangeEventDto ToDto() => ChangeEventDto.Build<UseAirPowerChangeEventDto>(this, Id);

    protected override List<ChangeEventAnimation> AfterAnimations => new()
    {
        new ShowCardAnnouncementAnimation(
            SourceCardId,
            $"{TriggeringFaction.WithPlayer()} uses {SourceCardState.CardName}")
    };

    public override void Mutate(BoardState board)
    {
        board.Of(SourceCardState).IsRevealed = true;
        board.DiscardCard(TriggeringFaction, SourceCardId);
    }

    public override string SummaryText() => $"{TriggeringFaction.WithPlayer()} used Air Power card {SourceCardState?.CardName}";
}
