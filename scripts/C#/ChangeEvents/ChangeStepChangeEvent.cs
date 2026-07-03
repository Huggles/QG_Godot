using System.Threading.Tasks;

public partial class ChangeStepChangeEvent : ChangeEvent
{
    public TurnStep NewStep { get; set; }

    public ChangeStepChangeEvent(TurnStep newStep) : base(Faction.NONE)
    {
        NewStep = newStep;
    }

    public override ChangeEventDto ToDto()
    {
        ChangeStepChangeEventDto dto = ChangeEventDto.Build<ChangeStepChangeEventDto>(this, Id);
        dto.NewStep = NewStep;
        return dto;
    }

    protected override Task<bool> ExecuteAsync()
    {
        var flow = GameFlow.Instance;
        flow.TurnStep = NewStep;
        flow.CardPlayRounds.Add(CardPlayRound.StartNew());
        return Task.FromResult(true);
    }

    public override bool ToHistoryItem => true;

    public override string SummaryText() => $"Turn step changed to {NewStep}";
}
