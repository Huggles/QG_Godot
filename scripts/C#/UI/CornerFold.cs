using Godot;

/// <summary>The amber folded corner that marks a recently drawn hand card. Top-left, because the fan covers each card's right side.</summary>
public partial class CornerFold : Control
{
	private const float LegFraction = 0.15f;
	private static readonly Color FoldColor = new("#fac775");
	private static readonly Color CreaseColor = new("#854f0b");
	private static readonly Color ShadowColor = new(0, 0, 0, 0.6f);

	public override void _Notification(int what)
	{
		if (what == NotificationResized) QueueRedraw();
	}

	public override void _Draw()
	{
		float leg = Size.X * LegFraction;
		float shadow = leg * 0.25f;
		DrawColoredPolygon(new[] { Vector2.Zero, new Vector2(leg + shadow, 0), new Vector2(0, leg + shadow) }, ShadowColor);
		Vector2[] fold = { Vector2.Zero, new Vector2(leg, 0), new Vector2(0, leg) };
		DrawColoredPolygon(fold, FoldColor);
		DrawPolyline(new[] { fold[0], fold[1], fold[2], fold[0] }, CreaseColor, Mathf.Max(2f, leg * 0.09f), true);
	}
}
