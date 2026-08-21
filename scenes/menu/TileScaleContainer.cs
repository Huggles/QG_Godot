using Godot;

/// <summary>
/// Container that automatically scales its direct children to fit.
/// For every child the largest uniform scale is picked for which the child's
/// combined minimum size still fits inside the container, after which the
/// child is stretched to fill the container exactly at that scale. The child
/// therefore always covers the container, while its internal layout (nine
/// patch margins, font sizes, ...) is drawn at the computed scale.
/// The scale is capped at 1.0, so children are only ever scaled down.
/// </summary>
[Tool]
[GlobalClass]
public partial class TileScaleContainer : Container
{
	private const float MinScale = 0.01f;
	private const float MaxScale = 1.0f;

	public override void _Ready()
	{
		ResizeChildren();
		ChildEnteredTree += OnChildEnteredTree;
	}

	public override void _Notification(int what)
	{
		if (what == NotificationSortChildren || what == NotificationResized)
		{
			ResizeChildren();
		}
	}

	private void OnChildEnteredTree(Node node)
	{
		ResizeChild(node);
	}

	/// <summary>
	/// Largest uniform scale at which <paramref name="minimumSize"/> still fits
	/// inside the container, never larger than 1.0. Axes without a minimum size
	/// do not constrain it.
	/// </summary>
	private float GetScaleFor(Vector2 minimumSize)
	{
		float scale = MaxScale;

		if (minimumSize.X > 0)
		{
			scale = Mathf.Min(scale, Size.X / minimumSize.X);
		}

		if (minimumSize.Y > 0)
		{
			scale = Mathf.Min(scale, Size.Y / minimumSize.Y);
		}

		// A child without a minimum size is left unscaled at MaxScale.
		return Mathf.Clamp(scale, MinScale, MaxScale);
	}

	private void ResizeChild(Node node)
	{
		if (node is not Control control || control.TopLevel)
		{
			return;
		}

		// Reset the scale first: GetCombinedMinimumSize() is expressed in the
		// child's own (unscaled) space, but a stale scale can still influence
		// the minimum size of nested children, so measure from a clean state.
		control.Scale = Vector2.One;

		float scale = GetScaleFor(control.GetCombinedMinimumSize());

		control.PivotOffset = Vector2.Zero;
		control.Position = Vector2.Zero;
		control.Size = Size / scale;
		control.Scale = new Vector2(scale, scale);
	}

	private void ResizeChildren()
	{
		foreach (Node node in GetChildren())
		{
			ResizeChild(node);
		}
	}
}
