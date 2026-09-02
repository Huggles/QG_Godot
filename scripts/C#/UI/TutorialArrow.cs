using Godot;

/// <summary>
/// The arrow a tutorial message can put on screen to say "look here". One at a time, aimed by a
/// <see cref="TutorialArrowData"/> — a viewport-relative point and a heading, not a node reference.
///
/// Lives as the LAST child of the Interface CanvasLayer, with z_index 160: one step above the highest
/// HUD element in that layer (the bottom-left menu, at 150), because an arrow that draws underneath
/// the thing it points at is worse than no arrow. Tree order alone cannot do it — z_index beats tree
/// order, and half this layer sets one.
///
/// A Sprite2D rather than a Control, which is what makes the aiming trivial: a Node2D rotates and
/// scales about its own origin, so parking the arrow's TIP on that origin — via
/// <see cref="Sprite2D.Offset"/>, below — means the node's position IS the point being indicated, at
/// every heading and every size. There is no pivot to keep in step and no rect to back out of the sum.
/// </summary>
public partial class TutorialArrow : Sprite2D
{
    /// <summary>
    /// The arrow in the scene tree, or null in a headless run. Callers must guard with
    /// <see cref="GodotObject.IsInstanceValid(GodotObject)"/>: a static handle to a freed Godot object
    /// throws on access rather than reading as null — the rule every static node handle here follows.
    /// </summary>
    public static TutorialArrow Current { get; private set; }

    /// <summary>
    /// How long the arrow is drawn, tip to tail, in pixels — applied as a uniform scale off the
    /// texture's real size, so it stays 150px whatever the source art is re-exported at. The image is
    /// square and the arrowhead does not fill its height, which is what leaves it looking right.
    /// </summary>
    [Export] public float ArrowLength { get; set; } = 150f;

    /// <summary>
    /// How far the drawn tip sits from the right edge of the source image, as a fraction of its width.
    /// The art has a little blank margin past the point; without this the arrow lands slightly short
    /// of the coordinate it was given.
    /// </summary>
    [Export] public float TipInset { get; set; } = 0.015f;

    /// <summary>How far the arrow slides along its own axis as it bobs, and how fast.</summary>
    private const float BobPixels = 10f;
    private const float BobSpeed = 3.4f;

    private const float FadeSeconds = 0.25f;

    private TutorialArrowData _spec;
    private Tween _fadeTween;
    private float _bobPhase;

    public override void _Ready()
    {
        Current = this;
        ApplyGeometry();

        Visible = false;
        Modulate = new Color(Modulate, 0f);
    }

    public override void _ExitTree()
    {
        if (Current == this) Current = null;
    }

    /// <summary>
    /// Put the drawn tip on the node's origin and size the sprite to <see cref="ArrowLength"/>.
    ///
    /// Uncentered, so the texture starts at the origin and <see cref="Sprite2D.Offset"/> can pull it
    /// back by exactly the tip's position within the image. Offset is in unscaled texture pixels — it
    /// is applied before Scale — so both numbers come off the texture's own size rather than off the
    /// drawn size.
    /// </summary>
    private void ApplyGeometry()
    {
        if (Texture == null) return;

        Vector2 textureSize = Texture.GetSize();
        if (textureSize.X <= 0f || textureSize.Y <= 0f) return;

        Centered = false;
        Offset = new Vector2(-textureSize.X * (1f - TipInset), -textureSize.Y / 2f);
        Scale = Vector2.One * (ArrowLength / textureSize.X);
    }

    /// <summary>
    /// Aim at the point <paramref name="spec"/> names and fade in, replacing whatever was showing.
    /// A null spec is the same as <see cref="Clear"/>.
    /// </summary>
    public void PointAt(TutorialArrowData spec)
    {
        if (spec == null) { Clear(); return; }

        _spec = spec;
        _bobPhase = 0f;

        // Placed before the fade so the first visible frame is already on target, rather than fading
        // in at wherever the previous message left it.
        UpdatePlacement();

        Visible = true;
        _fadeTween?.Kill();
        _fadeTween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _fadeTween.TweenProperty(this, "modulate:a", 1f, FadeSeconds);
    }

    /// <summary>Fade the arrow out. Safe to call when nothing is showing.</summary>
    public void Clear()
    {
        _spec = null;
        if (!Visible) return;

        _fadeTween?.Kill();
        _fadeTween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        _fadeTween.TweenProperty(this, "modulate:a", 0f, FadeSeconds);
        _fadeTween.Finished += () => Visible = false;
    }

    /// <summary>
    /// Only the bob needs a frame hook — the target is a fixed point, so unlike a node-following arrow
    /// there is nothing to chase. The placement is still recomputed each tick because the bob feeds
    /// into it, and because a window resize moves a viewport-relative point.
    /// </summary>
    public override void _Process(double delta)
    {
        if (_spec == null) return;

        _bobPhase += (float)delta * BobSpeed;
        UpdatePlacement();
    }

    private void UpdatePlacement()
    {
        Vector2 viewport = GetViewportRect().Size;
        Vector2 tip = new(_spec.X * viewport.X, _spec.Y * viewport.Y);

        float radians = Mathf.DegToRad(_spec.Rotation);
        // The source art points right, so a heading of 0 needs no correction anywhere.
        Vector2 heading = Vector2.Right.Rotated(radians);

        // Bob backwards along the heading, so the arrow nods at its target instead of drifting through
        // it. Sine shifted into 0..1 so the tip never overshoots the point it was given.
        float bob = (Mathf.Sin(_bobPhase) + 1f) * 0.5f * BobPixels;

        Rotation = radians;
        GlobalPosition = tip - heading * bob;
    }
}
