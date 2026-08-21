using Godot;

/// <summary>
/// NinePatchRect that always covers its parent control, whatever happens to its own size or scale.
///
/// The point is to let the scale be used purely as a visual knob: scaling this node up thickens the
/// nine patch borders instead of stretching the panel, because the rect compensates by shrinking its
/// own size to <c>parentSize / Scale</c>. The result on screen stays exactly the parent's rect.
///
/// It re-fits on every event that can break the match: its own resize, its own transform (scale)
/// changing, and the parent resizing. The parent should be a plain Control - a Container parent lays
/// its children out itself and would fight the size set here.
/// </summary>
[Tool]
[GlobalClass]
public partial class ParentSizedNinePatchRect : NinePatchRect
{
    private const float MinScale = 0.01f;

    private Control _parent;
    private bool _fitting;

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
            // Magnitude only: a negative scale mirrors the rect in place, it does not flip its size.
            float scaleX = Mathf.Max(Mathf.Abs(Scale.X), MinScale);
            float scaleY = Mathf.Max(Mathf.Abs(Scale.Y), MinScale);

            PivotOffset = Vector2.Zero;
            Position = Vector2.Zero;
            Size = new Vector2(_parent.Size.X / scaleX, _parent.Size.Y / scaleY);
        }
        finally
        {
            _fitting = false;
        }
    }
}
