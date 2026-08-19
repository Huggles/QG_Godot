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

    /// <summary>
    /// Kept out of the history strip. A step change fires several times a round and belongs to no
    /// faction, so it produced a run of flagless grey badges that pushed the entries a player actually
    /// wants — who deployed, who drew, who battled — out of the capped window.
    /// </summary>
    public override bool ToHistoryItem => false;

    public override string SummaryText() => $"Turn step changed to {NewStep}";
}
