using System.Threading.Tasks;

/// <summary>
/// Sets a faction's Score to a scenario-defined starting value at game setup. Unlike
/// <see cref="ScorePointsChangeEvent"/> this does NOT record a VPTurnSummary, so starting VP
/// counts toward totals without appearing as points "scored" in a round on the score screen.
/// Routed through a ChangeEvent (rather than a direct assignment) so the value replicates to
/// multiplayer clients, which never run scenario setup themselves.
/// </summary>
public partial class SetStartingScoreChangeEvent : ChangeEvent
{
    public int Score { get; set; }

    public SetStartingScoreChangeEvent(Faction faction, int score) : base(faction)
    {
        Score = score;
    }

    public override ChangeEventDto ToDto()
    {
        SetStartingScoreChangeEventDto dto = ChangeEventDto.Build<SetStartingScoreChangeEventDto>(this, Id);
        dto.Score = Score;
        return dto;
    }

    protected override async Task<bool> ExecuteAsync()
    {
        FactionState.ForEnum(TriggeringFaction).Score = Score;
        await Task.CompletedTask;
        return true;
    }

    public override string SummaryText() => $"{TriggeringFaction} starts with {Score} VP";
}
