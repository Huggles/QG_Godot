/// <summary>
/// The start of a faction's Air, Victory or Draw step, as the trigger of the window CardPlayRound.OpenStepStartWindow
/// opens for "use at the beginning of your X step" cards (Condition.AtStepStart). It changes nothing.
///
/// Applied only when a card can actually be used on it, so a game without such cards never sees one.
/// </summary>
public partial class StepStartChangeEvent : ChangeEvent
{
    public TurnStep Step { get; set; }

    public StepStartChangeEvent(TurnStep step, Faction faction) : base(faction)
    {
        Step = step;
    }

    public override ChangeEventDto ToDto()
    {
        StepStartChangeEventDto dto = ChangeEventDto.Build<StepStartChangeEventDto>(this, Id);
        dto.Step = Step;
        return dto;
    }

    public override void Mutate(BoardState board) { }

    /// <summary>Flow: nothing on the board or in the piles moves; the window's own recalculation reads the trigger.</summary>
    public override RecalcScope RecalcScope => RecalcScope.Flow;

    /// <summary>Kept out of the history strip, like ChangeStepChangeEvent: the card used on it is the entry worth showing.</summary>
    public override bool ToHistoryItem => false;

    public override string SummaryText() => $"{TriggeringFaction.WithPlayer()} begins its {Step} step";
}
