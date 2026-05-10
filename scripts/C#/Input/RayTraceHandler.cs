using Godot;
using System;

public partial class RayTraceHandler : Area3D
{
    private bool mousePreviouslyOverOpaque = false;
    private bool mouseCurrentlyOverOpaque = false;

    [Signal] public delegate void MouseEnterEventHandler(Node self);
    [Signal] public delegate void MouseExitEventHandler(Node self);
    [Signal] public delegate void MouseEnterOpaqueEventHandler(Node self);
    [Signal] public delegate void MouseExitOpaqueEventHandler(Node self);
    [Signal] public delegate void MouseSingleClickedOpaqueAreaEventHandler(Node self);
    [Signal] public delegate void MouseDoubleClickedOpaqueAreaEventHandler(Node self);

    public ClickableSprite3D ClickableSprite => GetParent() as ClickableSprite3D;

    public override void _Ready()
    {
        // Placeholder if needed later
    }

    public void OnStartHit(Node camera, InputEvent inputEvent, Vector3 inputPosition, Vector3 normal)
    {
        EmitSignal(SignalName.MouseEnter, this);
    }

    public void OnStopHit(Node camera, InputEvent inputEvent, Vector3 inputPosition, Vector3 normal)
    {
        EmitSignal(SignalName.MouseExit, this);
        EmitSignal(SignalName.MouseExitOpaque, this);
        mousePreviouslyOverOpaque = false;
    }

    public void OnHitting(Node camera, InputEvent inputEvent, Vector3 inputPosition, Vector3 normal)
    {
        mouseCurrentlyOverOpaque = ClickableSprite != null && ClickableSprite.IsPixelOpaque(inputPosition);

        if (!mousePreviouslyOverOpaque && mouseCurrentlyOverOpaque)
        {
            DebugUtilities.PrintPeer($"mouse_enter_opaque: {ClickableSprite.Identifier}");
            EmitSignal(SignalName.MouseEnterOpaque, this);
        }
        else if (mousePreviouslyOverOpaque && !mouseCurrentlyOverOpaque)
        {
            DebugUtilities.PrintPeer($"mouse_exit_opaque: {ClickableSprite.Identifier}");
            EmitSignal(SignalName.MouseExitOpaque, this);
        }

        if (inputEvent is InputEventMouseButton mouseButtonEvent)
        {
            if (mouseCurrentlyOverOpaque)
            {
                if (mouseButtonEvent.Pressed && mouseButtonEvent.ButtonIndex == MouseButton.Left)
                {
                    EmitSignal(SignalName.MouseSingleClickedOpaqueArea, this);
                }
                if (mouseButtonEvent.DoubleClick)
                {
                    EmitSignal(SignalName.MouseDoubleClickedOpaqueArea, this);
                }
            }
            else
            {
                // Button was pressed outside opaque area; currently no behavior.
            }
        }

        mousePreviouslyOverOpaque = mouseCurrentlyOverOpaque;
    }
}
