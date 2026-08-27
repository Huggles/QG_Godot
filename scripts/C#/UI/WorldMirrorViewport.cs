using Godot;

/// <summary>
/// Makes this SubViewport render the same 2D world as the viewport it is displayed in, turning it
/// into a second view of the board rather than an empty world of its own.
///
/// Every Viewport constructs its own World2D, so a SubViewport left alone renders only its own
/// children — a picture-in-picture set up that way shows nothing but whatever was parented under it.
/// World2D is a runtime object owned by the root Window and cannot be referenced from a .tscn, so
/// the hand-off has to happen in code.
///
/// Pair with <c>canvas_cull_mask</c> on this node and <c>visibility_layer</c> on the UI CanvasLayers:
/// sharing the world also pulls in every CanvasLayer in it — the HUD, and this viewport's own
/// container, which would then render itself recursively.
/// </summary>
public partial class WorldMirrorViewport : SubViewport
{
    public override void _Ready()
    {
        // GetParent() first: Node.GetViewport() called on a Viewport returns *itself*, so going
        // straight to GetViewport() here would assign this viewport's own world back to itself.
        World2D = GetParent().GetViewport().World2D;
    }
}
