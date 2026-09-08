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
        var summary = new VPTurnSummary(GameFlow.Instance.GameTurn, faction);
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

    /// <summary>
    /// Nothing derived reads the score. GameAPI.ScorePoints moves FactionState.Score, appends to
    /// GameFlow.VictoryPointSummaries (a log the victory screen reads at the end) and emits a UI
    /// signal — and no Condition, tag or card script anywhere consults any of the three. So the
    /// full six-faction recalculation this used to trigger produced, every time, exactly the tag
    /// state that was already there. It is one of the most frequent events in a game.
    /// </summary>
    public override RecalcScope RecalcScope => RecalcScope.None;

    public override string SummaryText() => $"{TriggeringFaction.WithPlayer()} scored {VPTurnSummary.TotalScore} points";

    
}
