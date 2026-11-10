using Godot;
using System.Linq;

/// <summary>
/// One bar per round across the bottom of a faction's flag: how many points the row gained that round.
/// Every row shares one scale (the best round anyone has had), so bar heights compare across the strip.
/// </summary>
public partial class VPSparkline : Control
{
	public Faction Faction { get; set; } = Faction.NONE;

	/// <summary>A round worth this many points fills the bar's height until someone does better.</summary>
	private const int MinimumScale = 5;
	private const float Gap = 1f;
	private const float MaxBarWidth = 6f;

	private static readonly Color Backing = new(0, 0, 0, 0.3f);
	private static readonly Color Track = new(1, 1, 1, 0.35f);
	private static readonly Color Bar = new(1, 1, 1, 0.9f);
	private static readonly Color CurrentBar = new(0.851f, 0.678f, 0.361f, 1f);
	private static readonly Color LossBar = new(0.9f, 0.3f, 0.3f, 1f);

	private int[] _points = System.Array.Empty<int>();
	private int _scale = MinimumScale;

	/// <summary>Re-reads the history. Cheap enough to call on every score or turn change.</summary>
	public void Refresh()
	{
		int rounds = VPHistory.CurrentRound;
		_points = VPHistory.PointsPerRound(Faction, rounds);
		_scale = StaticGameData.PlayableFactions
			.SelectMany(f => VPHistory.PointsPerRound(f, rounds))
			.Select(System.Math.Abs)
			.DefaultIfEmpty(0)
			.Max();
		_scale = System.Math.Max(_scale, MinimumScale);
		QueueRedraw();
	}

	public override void _Draw()
	{
		// One slot per round of the scenario, so the track fills left to right as the game goes on.
		int slots = System.Math.Max(GameFlow.Instance?.MaxRound ?? 20, _points.Length);
		float slot = (Size.X - 2) / slots;
		float barWidth = Mathf.Min(MaxBarWidth, slot - Gap);
		DrawRect(new Rect2(Vector2.Zero, Size), Backing);
		DrawLine(new Vector2(1, Size.Y - 1), new Vector2(Size.X - 1, Size.Y - 1), Track, 1f);

		float usable = Size.Y - 3;
		for (int i = 0; i < _points.Length; i++)
		{
			int pts = _points[i];
			if (pts == 0) continue;
			// A scoring round never draws thinner than a sliver, so one point still shows.
			float h = Mathf.Max(2f, usable * Mathf.Min(1f, System.Math.Abs(pts) / (float)_scale));
			Color color = pts < 0 ? LossBar : i == _points.Length - 1 ? CurrentBar : Bar;
			DrawRect(new Rect2(1 + i * slot + (slot - barWidth) / 2, Size.Y - 1 - h, barWidth, h), color);
		}
	}
}
