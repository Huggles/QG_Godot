using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>Points per source country, summed so two entries from one country show one number.</summary>
    public Dictionary<int, int> VpBySourceCountryId => SumBySource(e => e.SourceCountryVPs);

    /// <summary>Points per scoring unit, summed the same way.</summary>
    public Dictionary<int, int> VpBySourceUnitId => SumBySource(e => e.SourceUnitVPs);

    private Dictionary<int, int> SumBySource(Func<VPEntry, Dictionary<int, int>> sources) => VPTurnSummary.victoryPointEntries
        .Where(e => sources(e) != null)
        .SelectMany(sources)
        .GroupBy(kv => kv.Key)
        .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));

    public override void Mutate(BoardState board) => board.AddScore(VPTurnSummary.Faction, VPTurnSummary.TotalScore);

    /// <summary>The score pause and the VP labels run side by side; the event moves on once both are done.</summary>
    protected override async Task OnLiveMutatedAsync()
    {
        GameAPI.PresentScore(VPTurnSummary);
        await Task.WhenAll(Task.Delay(GameSettings.DurationLong), ShowSourceScores());
    }

    /// <summary>
    /// Started here rather than queued as an AfterAnimation, which would only begin after the pause above.
    /// Same skip as EnqueueAnimations: OnLiveMutatedAsync also runs on a save replay and on a headless server.
    /// </summary>
    private Task ShowSourceScores()
    {
        if (!PlayAnimations || GameContext.IsHeadless || ReplayContext.IsFastForwarding) return Task.CompletedTask;

        IEnumerable<Task> countries = VpBySourceCountryId
            .Select(kv => CountryState.ForId(kv.Key).CountryScene?.ShowVpScore(kv.Value) ?? Task.CompletedTask);
        IEnumerable<Task> units = VpBySourceUnitId
            .Select(kv => UnitState.ForId(kv.Key).UnitScene?.ShowVpScore(kv.Value) ?? Task.CompletedTask);
        return Task.WhenAll(countries.Concat(units));
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
