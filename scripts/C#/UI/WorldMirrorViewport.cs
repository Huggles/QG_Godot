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
/// Owns the whole cull-mask contract that goes with that, because sharing the world also pulls in
/// every CanvasLayer in it — the HUD, and this viewport's own container, which would then render
/// itself recursively. The layers below are the two halves of the answer: what this viewport renders,
/// and what the main window viewport does not.
/// </summary>
public partial class WorldMirrorViewport : SubViewport
{
    /// <summary>Bit 1 — the world. Every board node's default, rendered by both viewports.</summary>
    public const uint WorldLayer = 1;

    /// <summary>
    /// Bit 2 — the HUD. Carried by the Interface/modal/toast CanvasLayers so this viewport does not
    /// render the user interface it is displayed inside.
    /// </summary>
    public const uint UiLayer = 2;

    /// <summary>
    /// Bit 3 — drawn in this viewport and NOWHERE else. What a unit's indicator sits on while it
    /// belongs to the focus view alone: the main window viewport has this bit stripped from its cull
    /// mask below, so a node here is invisible on the board while still being drawn in the PiP.
    ///
    /// Culling is hierarchical — a CanvasItem is drawn only if it AND every parent shares a bit with
    /// the mask — so an indicator on this layer under a UnitScene on <see cref="WorldLayer"/> is drawn
    /// by a mask of WorldLayer|FocusOnlyLayer and pruned by a mask without bit 3.
    /// </summary>
    public const uint FocusOnlyLayer = 4;

    public override void _Ready()
    {
        // GetParent() first: Node.GetViewport() called on a Viewport returns *itself*, so going
        // straight to GetViewport() here would assign this viewport's own world back to itself.
        Viewport main = GetParent().GetViewport();
        World2D = main.World2D;

        // Set here rather than in the .tscn because the second line cannot be: the root Window's cull
        // mask is runtime state with no project setting behind it, and stripping the bit there is what
        // makes FocusOnlyLayer mean "focus only" at all.
        CanvasCullMask = WorldLayer | FocusOnlyLayer;
        main.CanvasCullMask &= ~FocusOnlyLayer;
    }
}
