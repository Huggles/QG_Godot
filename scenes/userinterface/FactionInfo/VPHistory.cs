using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Per-round victory points for a faction row, read off GameFlow's VP summaries. A row's figures fold in
/// the minor factions it acts for, the same way <see cref="FactionInfoRow"/> folds them into its score.
/// </summary>
public static class VPHistory
{
	/// <summary>The row's faction and the minor factions whose points it shows.</summary>
	public static IEnumerable<Faction> Scorers(Faction faction) => StaticGameData.MinorsOf(faction).Prepend(faction);

	/// <summary>The row's headline score: its own points plus its minor factions'.</summary>
	public static int CombinedScore(Faction faction) => Scorers(faction).Sum(f => FactionState.ForEnum(f)?.Score ?? 0);

	/// <summary>Every summary behind the row's score, oldest first.</summary>
	public static IEnumerable<VPTurnSummary> Summaries(Faction faction)
	{
		if (GameFlow.Instance == null) return Enumerable.Empty<VPTurnSummary>();
		return Scorers(faction).SelectMany(f => GameFlow.Instance.VictoryPointSummaries.GetValueOrDefault(f, new List<VPTurnSummary>()));
	}

	/// <summary>Points gained in each round 1..<paramref name="rounds"/>; index 0 is round 1.</summary>
	public static int[] PointsPerRound(Faction faction, int rounds)
	{
		int[] points = new int[System.Math.Max(rounds, 0)];
		foreach (VPTurnSummary summary in Summaries(faction))
			if (summary.Round >= 1 && summary.Round <= rounds)
				points[summary.Round - 1] += summary.TotalScore;
		return points;
	}

	/// <summary>
	/// Points the row held before any summary: scenario starting VP never produces one, so the chart
	/// starts from the score minus everything the summaries account for.
	/// </summary>
	public static int StartingPoints(Faction faction) => CombinedScore(faction) - Summaries(faction).Sum(s => s.TotalScore);

	/// <summary>The round the game is in now, or 0 before it has started.</summary>
	public static int CurrentRound => GameFlow.Instance?.Round ?? 0;
}
