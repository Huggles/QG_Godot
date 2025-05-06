using Godot;
using System;

public partial class DragInfo : GodotObject
{
    [Signal]
    public delegate void DragCompletedEventHandler(DragInfo data);

    public Control Source { get; set; }
    public Control Destination { get; set; }
    public Control Preview { get; set; }

    public DragInfo(Control source, Control preview)
    {
        Source = source;
        Preview = preview;

        if (Preview != null)
        {
            Preview.TreeExiting += OnTreeExiting;
        }
    }

    private void OnTreeExiting()
    {
        //EmitSignal(SignalName.DragCompleted, this);
    }
}
