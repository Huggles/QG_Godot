using Godot;

/// <summary>
/// An <see cref="OpaqueTextureRect"/> that also carries the "this is a selection target" behaviour
/// <see cref="ClickableSprite"/> gives an Area2D: show/hide, the radar sweep, a hover tint, and a
/// clickable/unclickable switch.
///
/// It exists because ClickableSprite has to reimplement mouse picking by hand — a RectangleShape2D
/// sized to the texture, a per-frame ToLocal, and a manual Image.GetPixel — and the target marker on
/// a unit is exactly where that hand-rolled picking went wrong: the collision shape is only ever
/// sized by SetTexture, which the unit marker never calls, so it hit-tested a 512x512 box against a
/// 1254x1254 texture. Godot's GUI picking asks <see cref="OpaqueTextureRect._HasPoint"/> instead, so
/// hover, exit and click are alpha-accurate from one place with no shape to keep in sync.
///
/// The API is deliberately the same as ClickableSprite's, so a scene can swap between the two
/// without the scene script changing.
/// </summary>
public partial class ClickableTextureRect : OpaqueTextureRect
{
    /// <summary>Tint while the cursor is on an opaque pixel of the marker.</summary>
    public static readonly Color HoverColor = new Color(0.5f, 1, 0.5f, 1);

    /// <summary>
    /// Resting tint of an ordinary offered target. Fully opaque, because the alpha it is actually
    /// drawn at comes from TargetRadarSweep.gdshader — a faint icon with an opaque slice sweeping over
    /// it — which multiplies into this.
    /// </summary>
    public static readonly Color SelectableColor = new Color(1, 1, 1, 1);

    /// <summary>
    /// Resting tint of a secondary target — one offered alongside the ordinary ones for a rarer
    /// action. Fainter so it reads as the unusual option without disappearing.
    /// See <see cref="SetClickableSubdued"/>.
    /// </summary>
    public static readonly Color SubduedColor = new Color(1, 1, 1, 0.3f);

    /// <summary>Alpha the marker returns to when the mouse leaves. Lowered by the subdued mode.</summary>
    private Color restingColor = SelectableColor;

    private bool isClickable;

    /// <summary>This marker's own copy of the radar shader. See <see cref="TargetRadar.CreateMaterial"/>.</summary>
    private ShaderMaterial radarMaterial;

    public override void _Ready()
    {
        base._Ready();
        radarMaterial = TargetRadar.CreateMaterial();
        Material = radarMaterial;
        Modulate = restingColor;
        MouseEntered += OnMouseEnteredOpaque;
        MouseExited += OnMouseExitedOpaque;
    }

    /// <summary>
    /// Hover and clicks alike are gated here rather than on the signals: a marker that is not being
    /// offered must not be the picked control at all, or it would swallow the hover from whatever
    /// sits behind it — the country silhouette under the unit — while showing nothing itself.
    /// </summary>
    public override bool _HasPoint(Vector2 point)
    {
        return isClickable && base._HasPoint(point);
    }

    public void ShowSprite() => Visible = true;

    public void HideSprite() => Visible = false;

    public void SetClickable()
    {
        restingColor = SelectableColor;
        Modulate = restingColor;
        isClickable = true;
        StartRadarSweep();
    }

    /// <summary>
    /// Clickable, but drawn as a secondary target: a flat faint alpha with NO radar sweep. Used for the
    /// rebuild-in-place deploy target on a unit, a rare option offered beside the ordinary ones that
    /// must not compete with them for attention.
    ///
    /// Deliberately not a fainter version of the sweep. Motion is what the eye goes to first, so a
    /// slice turning over a dimmer icon would still be read before the ordinary targets beside it.
    /// Holding still is the clearer signal: the ordinary targets are the ones that sweep. Hover still
    /// brightens to <see cref="HoverColor"/>, so the marker is unambiguous under the mouse.
    /// </summary>
    public void SetClickableSubdued()
    {
        restingColor = SubduedColor;
        StopRadarSweep();
        Modulate = restingColor;
        isClickable = true;
    }

    public void SetUnclickable()
    {
        restingColor = SelectableColor;
        isClickable = false;
        StopRadarSweep();
        Modulate = restingColor;
    }

    public void StartRadarSweep() => TargetRadar.Start(radarMaterial);

    public void StopRadarSweep() => TargetRadar.Stop(radarMaterial);

    /// <summary>
    /// MouseEntered on a plain TextureRect fires anywhere in the rect; here <see cref="_HasPoint"/>
    /// has already restricted picking to opaque pixels, so the engine's own signal is the
    /// alpha-accurate one and there is nothing left to re-check.
    ///
    /// The radar sweep is left running: it lives in the shader and multiplies whatever alpha modulate
    /// carries, so the hover hue and the turning slice coexist instead of overwriting each other.
    /// </summary>
    private void OnMouseEnteredOpaque()
    {
        Modulate = HoverColor;
    }

    private void OnMouseExitedOpaque()
    {
        // The resting colour, not SelectableColor: a subdued target that has been hovered once must
        // fall back to being subdued, otherwise it is left as prominent as an ordinary target.
        Modulate = restingColor;
    }
}
