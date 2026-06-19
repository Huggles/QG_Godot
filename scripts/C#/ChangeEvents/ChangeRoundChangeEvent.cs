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
        await Task.CompletedTask;
        return true;
    }

    public override bool ToHistoryItem => true;

    public override string SummaryText() => $"Game turn advanced to {NewTurn}";
}
