using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Every faction row's running score, one line each, round by round. The selected faction is drawn
/// heavy with a dot per round; the rest stay thin so it reads against them.
/// </summary>
public partial class ScoreChart : Control
{
	private static readonly Color GridColor = new(0.93f, 0.9f, 0.82f, 0.12f);
	private static readonly Color AxisText = new(0.93f, 0.9f, 0.82f, 0.6f);
	private static readonly Color Brass = new(0.851f, 0.678f, 0.361f, 1f);

	private const float LeftPad = 34f;
	private const float BottomPad = 22f;
	private const float TopPad = 8f;
	private const float RightPad = 10f;

	private readonly Dictionary<Faction, int[]> _series = new();
	private Faction _selected = Faction.NONE;
	private int _rounds = 1;

	/// <summary>Reads every row's history afresh and highlights <paramref name="selected"/>.</summary>
	public void Show(Faction selected)
	{
		_selected = selected;
		_rounds = System.Math.Max(VPHistory.CurrentRound, 1);
		_series.Clear();
		foreach (Faction faction in StaticGameData.PlayableFactions)
		{
			int[] perRound = VPHistory.PointsPerRound(faction, _rounds);
			int[] running = new int[_rounds + 1];
			running[0] = VPHistory.StartingPoints(faction);
			for (int r = 1; r <= _rounds; r++) running[r] = running[r - 1] + perRound[r - 1];
			_series[faction] = running;
		}
		QueueRedraw();
	}

	/// <summary>The faction's own colour, lifted when it is too dark to see on the panel (Germany's black).</summary>
	public static Color LineColor(Faction faction)
	{
		Color c = StaticGameData.FactionDataMap[faction].FactionColor;
		return c.Luminance < 0.3f ? c.Lerp(Colors.White, 0.5f) : c;
	}

	public override void _Draw()
	{
		if (_series.Count == 0) return;
		Font font = GetThemeDefaultFont() ?? ThemeDB.FallbackFont;
		const int fontSize = 13;

		int max = _series.Values.SelectMany(v => v).DefaultIfEmpty(0).Max();
		int min = System.Math.Min(0, _series.Values.SelectMany(v => v).DefaultIfEmpty(0).Min());
		int step = max - min > 40 ? 10 : 5;
		int top = System.Math.Max(step * 2, (max + step - 1) / step * step);
		int bottom = min >= 0 ? 0 : -((-min + step - 1) / step * step);

		Rect2 plot = new(LeftPad, TopPad, Size.X - LeftPad - RightPad, Size.Y - TopPad - BottomPad);
		Vector2 At(int round, int vp) => new(
			plot.Position.X + plot.Size.X * round / _rounds,
			plot.End.Y - plot.Size.Y * (vp - bottom) / (float)(top - bottom));

		// Horizontal grid with VP labels, then a tick per round.
		for (int vp = bottom; vp <= top; vp += step)
		{
			float y = At(0, vp).Y;
			DrawLine(new Vector2(plot.Position.X, y), new Vector2(plot.End.X, y), GridColor, 1f);
			DrawString(font, new Vector2(0, y + fontSize * 0.35f), vp.ToString(), HorizontalAlignment.Right, LeftPad - 6, fontSize, AxisText);
		}
		int labelEvery = _rounds > 12 ? 2 : 1;
		for (int r = 0; r <= _rounds; r++)
		{
			Vector2 p = At(r, bottom);
			DrawLine(p, p + new Vector2(0, 4), GridColor, 1f);
			if (r % labelEvery == 0)
				DrawString(font, new Vector2(p.X - 20, plot.End.Y + 17), r == 0 ? "Start" : r.ToString(), HorizontalAlignment.Center, 40, fontSize, AxisText);
		}

		// Unselected lines first so the selected one is drawn on top.
		foreach ((Faction faction, int[] values) in _series.OrderBy(kv => kv.Key == _selected))
		{
			bool selected = faction == _selected;
			Color color = LineColor(faction);
			if (!selected) color.A = 0.45f;
			Vector2[] points = values.Select((vp, r) => At(r, vp)).ToArray();
			if (points.Length > 1) DrawPolyline(points, color, selected ? 3f : 1.5f, true);
			if (!selected) continue;
			foreach (Vector2 p in points)
			{
				DrawCircle(p, 4.5f, Brass);
				DrawCircle(p, 3f, color);
			}
		}
	}
}
