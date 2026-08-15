using System.Threading.Tasks;

public partial class ChangeRoundChangeEvent : ChangeEvent
{
    public int NewTurn { get; set; }

    public ChangeRoundChangeEvent(int newTurn) : base(Faction.NONE)
    {
        NewTurn = newTurn;
    }

    public override ChangeEventDto ToDto()
    {
        ChangeRoundChangeEventDto dto = ChangeEventDto.Build<ChangeRoundChangeEventDto>(this, Id);
        dto.NewTurn = NewTurn;
        return dto;
    }

    protected override async Task<bool> ExecuteAsync()
    {
        GameFlow.Instance.GameTurn = NewTurn;
        GameFlow.Instance.CardsPlayedThisTurnStep.Clear();
        // After GameTurn moved, so CurrentFaction is the faction whose turn is starting.
        GameFlow.Instance.DropOwnTurnReactionSkip();
        await Task.CompletedTask;
        return true;
    }

    public override bool ToHistoryItem => true;

    public override string SummaryText() => $"Game turn advanced to {NewTurn}";
}
