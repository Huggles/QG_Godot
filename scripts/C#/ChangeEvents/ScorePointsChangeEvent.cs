using Godot;
using System;
using System.Threading.Tasks;

public partial class ScorePointsChangeEvent : ChangeEvent
{    
    public VPTurnSummary VPTurnSummary { get; set; }

    public ScorePointsChangeEvent(VPTurnSummary vPTurnSummary) : base(vPTurnSummary.Faction)
    {
        VPTurnSummary = vPTurnSummary;
    }

    public ScorePointsChangeEvent(VPEntry vpEntry, Faction faction) : base(faction)
    {
        var summary = new VPTurnSummary(GameFlow.Instance.GameTurn);
        summary.AddScore(vpEntry);
        VPTurnSummary = summary;
    }

    public override ChangeEventDto ToDto()
    {
        ScorePointsChangeEventDto dto = ChangeEventDto.Build<ScorePointsChangeEventDto>(this, Id);
        dto.VPTurnSummary = VPTurnSummary;
        return dto;
    }

    protected override async Task<bool> ExecuteAsync()
    {
        GameAPI.ScorePoints(VPTurnSummary);        
        await Task.Delay(GameSettings.DurationLong);
        return true;
    }

    public override string SummaryText() => $"{TriggeringFaction} scored {VPTurnSummary.TotalScore} points";

    
}
