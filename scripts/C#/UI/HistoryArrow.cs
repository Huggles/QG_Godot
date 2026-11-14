using Godot;

/// <summary>
/// The thin gold arrow between a history entry's source card and its targets. Drawn in the
/// GlassPopupPanel's border gold so it reads as part of the modal's chrome rather than a game piece.
/// </summary>
public partial class HistoryArrow : Control
{
	/// <summary>GlassPopupPanel's border_color, at a higher alpha: a 1px hairline can be faint, a lone arrow cannot.</summary>
	private static readonly Color StrokeColor = new(0.851f, 0.678f, 0.361f, 0.85f);
	private const float StrokeWidth = 2f;
	/// <summary>Half-height of the open chevron head, as a fraction of the control's width.</summary>
	private const float HeadFraction = 0.3f;

	public override void _Notification(int what)
	{
		if (what == NotificationResized) QueueRedraw();
	}

	public override void _Draw()
	{
		float mid = Size.Y * 0.5f;
		float inset = StrokeWidth;
		Vector2 tail = new(inset, mid);
		Vector2 tip = new(Size.X - inset, mid);
		float head = Size.X * HeadFraction;

		DrawLine(tail, tip, StrokeColor, StrokeWidth, true);
		DrawPolyline(new[] { tip + new Vector2(-head, -head), tip, tip + new Vector2(-head, head) },
			StrokeColor, StrokeWidth, true);
	}
}
