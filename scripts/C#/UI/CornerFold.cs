using Godot;

/// <summary>The amber folded corner that marks a recently drawn hand card. Top-left, because the fan covers each card's right side.</summary>
public partial class CornerFold : Control
{
	private const float LegFraction = 0.24f;
	private static readonly Color FoldColor = new("#fac775");
	private static readonly Color CreaseColor = new("#ef9f27");

	public override void _Notification(int what)
	{
		if (what == NotificationResized) QueueRedraw();
	}

	public override void _Draw()
	{
		float leg = Size.X * LegFraction;
		DrawColoredPolygon(new[] { Vector2.Zero, new Vector2(leg, 0), new Vector2(0, leg) }, FoldColor);
		DrawLine(new Vector2(leg, 0), new Vector2(0, leg), CreaseColor, Mathf.Max(2f, leg * 0.06f), true);
	}
}
