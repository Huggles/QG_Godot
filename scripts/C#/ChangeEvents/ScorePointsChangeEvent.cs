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

    /// <summary>
    /// Points per source country, summed so two entries from one country show one number. An entry that
    /// names neither a country nor a unit lands on the scoring faction's home space; a zero one shows nothing.
    /// </summary>
    public Dictionary<int, int> VpBySourceCountryId => SumBySource(e => e.SourceCountryVPs ?? HomeSpaceFallback(e));

    private Dictionary<int, int> HomeSpaceFallback(VPEntry entry)
    {
        if (entry.SourceUnitVPs != null || entry.VictoryPoints == 0) return null;
        CountryState home = StaticGameData.FactionDataMap[VPTurnSummary.Faction].HomeSpaceCountryState;
        return home == null ? null : new Dictionary<int, int> { [home.Id] = entry.VictoryPoints };
    }

    /// <summary>Points per scoring unit, summed the same way.</summary>
    public Dictionary<int, int> VpBySourceUnitId => SumBySource(e => e.SourceUnitVPs);

    private Dictionary<int, int> SumBySource(Func<VPEntry, Dictionary<int, int>> sources) => VPTurnSummary.victoryPointEntries
        .Where(e => sources(e) != null)
        .SelectMany(sources)
        .GroupBy(kv => kv.Key)
        .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));

    public override void Mutate(BoardState board) => board.AddScore(VPTurnSummary.Faction, VPTurnSummary.TotalScore);

    private Task _presentation = Task.CompletedTask;

    /// <summary>
    /// Starts the score pause and the VP labels side by side but does not await them: this runs before
    /// the broadcast, so awaiting here held the clients back until the host had finished.
    /// </summary>
    protected override Task OnLiveMutatedAsync()
    {
        GameAPI.PresentScore(VPTurnSummary);
        _presentation = Task.WhenAll(Task.Delay(GameSettings.DurationLong), ShowSourceScores());
        return Task.CompletedTask;
    }

    /// <summary>The wait for the presentation started above, now after the broadcast.</summary>
    protected override List<ChangeEventAnimation> AfterAnimations => new() { new AwaitTaskAnimation(_presentation) };

    /// <summary>
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
