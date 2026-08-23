using Godot;

/// <summary>
/// A TextureRect that is only "under the mouse" where its texture is actually opaque.
///
/// A country silhouette fills a fraction of its rect - the rest is transparent, and neighbouring
/// countries' rects overlap it. Plain MouseEntered fires on the rect, so hovering empty space next
/// to Africa still counted as hovering Africa, and whichever rect happened to be on top swallowed
/// the hover from the country the mouse was really over.
///
/// This is the Control equivalent of what <see cref="ClickableSprite"/> does for Area2D, except the
/// engine does the work: <see cref="_HasPoint"/> is what Godot's GUI picking asks before it hands a
/// control the hover, so MouseEntered, MouseExited and GuiInput all become alpha-accurate at once,
/// and a transparent pixel falls through to whatever sits behind.
/// </summary>
public partial class OpaqueTextureRect : TextureRect
{
    /// <summary>
    /// Alpha above which a pixel counts as part of the shape. Not zero: imported textures carry a
    /// fringe of near-transparent pixels around the silhouette from mipmapping and premultiplied
    /// edges, which would otherwise extend the hover area by a few pixels of nothing.
    /// </summary>
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float AlphaThreshold { get; set; } = 0.1f;

    /// <summary>Left press on a pixel of the shape itself, never on the transparent rest of the rect.</summary>
    [Signal] public delegate void MouseLeftClickOnOpaqueEventHandler();

    /// <summary>Right press on a pixel of the shape itself. <see cref="MouseLeftClickOnOpaque"/>.</summary>
    [Signal] public delegate void MouseRightClickOnOpaqueEventHandler();

    private Image image;
    private Texture2D imageSourceTexture;

    /// <summary>
    /// CPU-side copy of the texture, the only way to read a pixel back. Cached because
    /// <see cref="Texture2D.GetImage"/> pulls from the GPU, and this is asked once per mouse motion.
    /// </summary>
    private Image TextureImage
    {
        get
        {
            if (Texture == null)
                return null;

            // Keyed on the texture itself, not just on null: CountryScene.SpawnCountry assigns the
            // real country texture over whatever Country.tscn shipped with, and a cache that only
            // filled once would hit-test every country against that placeholder.
            if (image == null || imageSourceTexture != Texture)
            {
                imageSourceTexture = Texture;
                image = Texture.GetImage();
                if (image != null && image.IsCompressed())
                    image.Decompress();
            }
            return image;
        }
    }

    public override void _Ready()
    {
        // Ignore is skipped by picking entirely, so _HasPoint would never be asked and MouseEntered
        // would never fire. Pass keeps the event travelling on to whatever is behind, which is what a
        // decorative silhouette wants; Stop is left alone in case a scene meant to claim the input.
        if (MouseFilter == MouseFilterEnum.Ignore)
            MouseFilter = MouseFilterEnum.Pass;
    }

    /// <summary>
    /// Clicks arrive here only while the cursor is on an opaque pixel: the control is not the picked
    /// one otherwise, because <see cref="_HasPoint"/> said the cursor was not over it. So the alpha
    /// test the signals promise is the same one that governs the hover, with nothing to re-check.
    ///
    /// Emits on press rather than on press and release both, so one click is one signal.
    ///
    /// Deliberately no AcceptEvent(): the click has to carry on to the physics picking behind this
    /// control, which is what drives the <see cref="ClickableSprite"/> target markers sitting on the
    /// same country. Swallowing it here would make a country unselectable while its silhouette is
    /// hovered - precisely when the player means to click it.
    /// </summary>
    public override void _GuiInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseButton mouseButton || !mouseButton.Pressed)
            return;

        if (mouseButton.ButtonIndex == MouseButton.Left)
            EmitSignal(SignalName.MouseLeftClickOnOpaque);
        else if (mouseButton.ButtonIndex == MouseButton.Right)
            EmitSignal(SignalName.MouseRightClickOnOpaque);
    }

    /// <summary>
    /// True only where the texture is opaque under <paramref name="point"/> (control-local pixels).
    /// Falls back to the plain rect when there is no readable texture, so a control without one
    /// behaves like an ordinary TextureRect rather than becoming unhoverable.
    /// </summary>
    public override bool _HasPoint(Vector2 point)
    {
        Image img = TextureImage;
        if (img == null)
            return new Rect2(Vector2.Zero, Size).HasPoint(point);

        if (!TryGetTexel(point, img.GetSize(), out Vector2I texel))
            return false;

        return img.GetPixelv(texel).A > AlphaThreshold;
    }

    /// <summary>
    /// Maps a control-local point to the texture pixel drawn there, honouring the stretch mode - the
    /// rect and the texture are not the same size or even the same aspect in general, so a straight
    /// point-to-pixel read would sample the wrong part of the shape. False when the point lands
    /// outside the drawn image (the letterbox bars of a keep-aspect fit, say).
    /// </summary>
    private bool TryGetTexel(Vector2 point, Vector2 textureSize, out Vector2I texel)
    {
        texel = Vector2I.Zero;
        if (textureSize.X <= 0 || textureSize.Y <= 0)
            return false;

        Vector2 uv;
        if (StretchMode == StretchModeEnum.Tile)
        {
            // Every repeat shows the whole texture, so wrap rather than fit.
            uv = new Vector2(Mathf.PosMod(point.X, textureSize.X), Mathf.PosMod(point.Y, textureSize.Y)) / textureSize;
        }
        else
        {
            Vector2 drawSize = textureSize;
            Vector2 drawPos = Vector2.Zero;

            switch (StretchMode)
            {
                case StretchModeEnum.Scale:
                    drawSize = Size;
                    break;
                case StretchModeEnum.KeepCentered:
                    drawPos = (Size - textureSize) / 2f;
                    break;
                case StretchModeEnum.KeepAspect:
                case StretchModeEnum.KeepAspectCentered:
                case StretchModeEnum.KeepAspectCovered:
                    // Covered fills the rect and overflows; the other two fit inside it.
                    float ratio = StretchMode == StretchModeEnum.KeepAspectCovered
                        ? Mathf.Max(Size.X / textureSize.X, Size.Y / textureSize.Y)
                        : Mathf.Min(Size.X / textureSize.X, Size.Y / textureSize.Y);
                    drawSize = textureSize * ratio;
                    // KeepAspect alone anchors top-left; the other two centre what they draw.
                    if (StretchMode != StretchModeEnum.KeepAspect)
                        drawPos = (Size - drawSize) / 2f;
                    break;
                // StretchModeEnum.Keep draws at top-left at native size: the defaults above.
            }

            if (drawSize.X <= 0 || drawSize.Y <= 0)
                return false;

            uv = (point - drawPos) / drawSize;
        }

        if (uv.X < 0f || uv.X >= 1f || uv.Y < 0f || uv.Y >= 1f)
            return false;

        if (FlipH) uv.X = 1f - uv.X;
        if (FlipV) uv.Y = 1f - uv.Y;

        texel = (Vector2I)(uv * textureSize);
        texel = texel.Clamp(Vector2I.Zero, (Vector2I)textureSize - Vector2I.One);
        return true;
    }
}
