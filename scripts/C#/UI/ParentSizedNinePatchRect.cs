using Godot;

/// <summary>
/// NinePatchRect that always covers its parent control, whatever happens to its own size or scale.
///
/// The point is to make scale a purely visual knob: turning <see cref="TileScale"/> up thickens the
/// nine patch borders instead of stretching the panel, because the rect compensates by shrinking its
/// own size to <c>parentSize / TileScale</c>. The result on screen stays exactly the parent's rect.
/// Set the scale through <see cref="TileScale"/>, never through the node's Scale.
///
/// It re-fits on every event that can break the match: its own resize, its own transform changing,
/// and the parent resizing. A Container parent normally fights this, since it sizes its children
/// itself - <see cref="MenuPanel"/> is the exception and deliberately skips this node.
/// </summary>
[Tool]
[GlobalClass]
public partial class ParentSizedNinePatchRect : NinePatchRect
{
	private const float MinScale = 0.01f;

	private Control _parent;
	private bool _fitting;
	private float _tileScale = 1.0f;

	/// <summary>
	/// How large the frame art is drawn, and the only place to set it: this replaces the node's
	/// Scale, which is not usable here. A Container parent greys Scale out in the inspector and
	/// <c>fit_child_in_rect</c> resets it to 1 on every layout pass, so a scale typed in there would
	/// silently vanish. An exported property survives both, and <see cref="FitToParent"/> writes it
	/// back into Scale. That is not a defence against a container that does lay this node out - such
	/// a container gets the last word on Scale, which is what the configuration warning is about.
	///
	/// Lower means smaller borders and a smaller panel: the nine patch cannot draw thinner than its
	/// own patch margins, so this scales that floor down with it.
	/// </summary>
	[Export(PropertyHint.Range, "0.01,2,0.01,or_greater")]
	public float TileScale
	{
		get => _tileScale;
		set
		{
			_tileScale = Mathf.Max(value, MinScale);
			FitToParent();
		}
	}

	public override void _EnterTree()
	{
		// Transform notifications are opt-in, and they are what catches a scale change - unlike a
		// resize, assigning Scale emits nothing on its own.
		SetNotifyTransform(true);
		TrackParent();
		FitToParent();
	}

	public override void _ExitTree()
	{
		UntrackParent();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized || what == NotificationTransformChanged)
		{
			FitToParent();
		}
	}

	public override string[] _GetConfigurationWarnings()
	{
		Node parent = GetParent();

		// MenuPanel is a Container, but it deliberately leaves this node out of its layout pass
		// precisely so the sizing below can stand.
		if (parent is Container && parent is not MenuPanel)
		{
			return new[]
			{
                "The parent is a Container, which positions and sizes its children itself. "
				+ "This node's size will be overwritten by the container's layout."
			};
		}

		if (parent is not Control)
		{
			return new[] { "This node needs a Control parent to take its size from." };
		}

		return System.Array.Empty<string>();
	}

	private void TrackParent()
	{
		_parent = GetParent() as Control;

		if (_parent != null)
		{
			_parent.Resized += FitToParent;
		}
	}

	private void UntrackParent()
	{
		if (_parent != null)
		{
			_parent.Resized -= FitToParent;
			_parent = null;
		}
	}

	private void FitToParent()
	{
		// Setting Size and Position below re-enters through the resize and transform notifications.
		if (_fitting || _parent == null)
		{
			return;
		}

		_fitting = true;

		try
		{
			PivotOffset = Vector2.Zero;
			Position = Vector2.Zero;
			Scale = new Vector2(_tileScale, _tileScale);
			Size = _parent.Size / _tileScale;
		}
		finally
		{
			_fitting = false;
		}
	}
}
