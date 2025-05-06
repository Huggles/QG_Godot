using Godot;
using System;

public partial class DragHandler : Control
{
    [Export]
    public Vector2 PreviewScale = Vector2.One;

    private Control _targetNode;

    public bool Draggable = false;
    public bool IsInsideDropable = false;
    public bool DroppedOnTarget = false;
    public Variant BodyRef;

    private int _originalZIndex;

    public override void _Ready()
    {
        var parentNode = GetParent();

        if (parentNode is Control control)
        {
            _targetNode = control;
            _targetNode.Connect("mouse_entered", new Callable(this, nameof(OnMouseEntered)));
            _targetNode.Connect("mouse_exited", new Callable(this, nameof(OnMouseExited)));
            AddToGroup("DRAGGABLE");
        }
        else
        {
            GD.PrintErr("Could not determine draggable node");
        }
    }

    private void OnMouseEntered()
    {
        _targetNode.Scale = new Vector2(1.05f, 1.05f);
        _originalZIndex = ((Control)_targetNode).ZIndex;
        ((Control)_targetNode).ZIndex = 100;
    }

    private void OnMouseExited()
    {
        _targetNode.Scale = new Vector2(1f, 1f);
        ((Control)_targetNode).ZIndex = _originalZIndex;
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var dragData = new DragInfo((Control)_targetNode, CreateItemPreview());
        SetDragPreview(dragData.Preview);
        return dragData;
    }

    private Control CreateItemPreview()
    {
        var preview = (Control)_targetNode.Duplicate();
        preview.Scale = PreviewScale;
        preview.PivotOffset = new Vector2(-preview.Size.X / 2f, -preview.Size.Y / 2f);
        return preview;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return true;// data is DragInfo;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        // if (data is not DragInfo dragData)
        //     return;

        // dragData.Destination = this;
        // dragData.Source?.RemoveItem(dragData.Item);
    }
}
