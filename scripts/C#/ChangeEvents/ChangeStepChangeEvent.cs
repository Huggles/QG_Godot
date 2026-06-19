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

    protected override async Task<bool> ExecuteAsync()
    {
        GameFlow.Instance.TurnStep = NewStep;
        await Task.CompletedTask;
        return true;
    }

    public override bool ToHistoryItem => true;

    public override string SummaryText() => $"Turn step changed to {NewStep}";
}
