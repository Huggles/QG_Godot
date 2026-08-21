using Godot;

/// <summary>
/// Root of <c>res://scenes/menu/MenuPanel.tscn</c>: the nine-patch background frame that menu modals
/// sit in, with a <c>ContentContainer</c> the caller fills.
///
/// This is a Container purely for the minimum-size plumbing. The panel has to report the content's
/// minimum size as its own, or a parent that sizes to content — the shell's CenterContainer, the
/// PanelContainer root of PresentationModal — collapses the panel and the content spills out. Doing
/// that from a plain Control means hand-wiring <c>MinimumSizeChanged</c>, which only propagates one
/// level and silently misses a content change that lands before the wiring is up; Container gets the
/// same signal from every child for free and pushes the result up the tree, which is what
/// <see cref="ModalStack"/> relies on when it lays modals out by their combined minimum size.
///
/// Being a Container means laying children out here, since a container child ignores its own anchors.
/// Every child is filled to the panel's rect — what the anchors used to do — except the background,
/// which sizes itself against this node's rect and its own scale (see
/// <see cref="ParentSizedNinePatchRect"/>) and would only fight a layout pass.
/// </summary>
[Tool]
[GlobalClass]
public partial class MenuPanel : Container
{
    /// <summary>Where the panel's contents go; the margins on it keep them off the nine-patch border.</summary>
    public MarginContainer ContentContainer => GetNodeOrNull<MarginContainer>("ContentContainer");

    public override void _Notification(int what)
    {
        if (what == NotificationSortChildren)
        {
            SortContent();
        }
    }

    /// <summary>
    /// The content drives the panel's size, but not below what the frame art needs: a nine patch
    /// cannot draw thinner than its own patch margins, so a panel smaller than that would have its
    /// border hanging outside the panel. Only the background's *own* minimum counts here - it is
    /// measured in its unscaled space, so scaling the background down lowers this floor, which is
    /// how a panel gets to be smaller than its border art is wide.
    /// </summary>
    public override Vector2 _GetMinimumSize()
    {
        Vector2 minimum = ContentContainer?.GetCombinedMinimumSize() ?? Vector2.Zero;

        foreach (Node child in GetChildren())
        {
            if (child is not ParentSizedNinePatchRect background)
            {
                continue;
            }

            // TileScale, not Scale: the background owns Scale and may not have written this frame's
            // value into it yet, whereas TileScale is the authored setting.
            Vector2 backgroundMinimum = background.GetCombinedMinimumSize() * background.TileScale;

            minimum = new Vector2(
                Mathf.Max(minimum.X, backgroundMinimum.X),
                Mathf.Max(minimum.Y, backgroundMinimum.Y)
            );
        }

        return minimum;
    }

    private void SortContent()
    {
        var rect = new Rect2(Vector2.Zero, Size);

        foreach (Node child in GetChildren())
        {
            if (child is not Control control || control.TopLevel)
            {
                continue;
            }

            if (control is ParentSizedNinePatchRect)
            {
                continue;
            }

            FitChildInRect(control, rect);
        }
    }
}
