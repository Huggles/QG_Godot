using Godot;
using System;

public partial class ClickableSprite3D : Sprite3D
{
    // Properties and fields
    private bool mouseOver = false;
    public string Identifier { get; set; }

    private Texture2D _clickableTexture;
    public Texture2D ClickableTexture
    {
        get => _clickableTexture;
        set
        {
            _clickableTexture = value;
            Texture = value;
        }
    }

    private Image image;
    private Color selectableColor = Colors.White;
    private Color hoverColor = Colors.Green;

    // Nodes
    private CollisionShape3D collisionShape;
    private Node3D clickableSpriteArea;

    // Signals
    [Signal] public delegate void MouseEnterEventHandler(ClickableSprite3D sprite);
    [Signal] public delegate void MouseExitEventHandler(ClickableSprite3D sprite);
    [Signal] public delegate void MouseEnterOpaqueEventHandler(ClickableSprite3D sprite);
    [Signal] public delegate void MouseExitOpaqueEventHandler(ClickableSprite3D sprite);
    [Signal] public delegate void MouseLeftClickOpaqueEventHandler(ClickableSprite3D sprite);
    [Signal] public delegate void MouseLeftDoubleClickOpaqueEventHandler(ClickableSprite3D sprite);

    // Static scene creation
    private static readonly PackedScene ClickableSpriteScene = GD.Load<PackedScene>("res://scenes/clickable_sprite_3d.tscn");

    public static ClickableSprite3D CreateWithTexture(Texture2D texture)
    {
        var sprite = ClickableSpriteScene.Instantiate<ClickableSprite3D>();
        sprite.ClickableTexture = texture;
        return sprite;
    }

    public override void _Ready()
    {
        clickableSpriteArea = GetNode<Node3D>("ClickableSpriteArea");
        collisionShape = clickableSpriteArea.GetNode<CollisionShape3D>("CollisionShape3D");

        // Duplicate collision shape for safe runtime use
        collisionShape.Shape = (collisionShape.Shape.Duplicate() as Shape3D)!;

        SetGlowColor(selectableColor);
        Disable();
        SetCollisionShape();
    }

    public void Enable()
    {
        Visible = true;
        collisionShape.Disabled = false;
    }

    public void Disable()
    {
        Visible = false;
        collisionShape.Disabled = true;
    }

    private void OnTextureChanged()
    {
        if (_clickableTexture != null)
        {
            MaterialOverride?.Set("shader_parameter/selectable_texture", _clickableTexture);

            image = _clickableTexture.GetImage();
            if (image != null && image.IsCompressed())
                image.Decompress();

            SetCollisionShape();
        }
    }

    private void SetCollisionShape()
    {
        if (collisionShape != null && _clickableTexture != null)
        {
            var shape = collisionShape.Shape as BoxShape3D;
            if (shape != null)
            {
                shape.Size = new Vector3(
                    _clickableTexture.GetWidth() * PixelSize,
                    _clickableTexture.GetHeight() * PixelSize,
                    shape.Size.Z
                );
            }
        }
    }

    public bool IsPixelOpaque(Vector3 inputPosition)
    {
        if (image == null) return false;

        Vector3 pixelPosition = (inputPosition - GlobalPosition) / PixelSize;

        float textureLocalX = pixelPosition.X + (_clickableTexture.GetWidth() / 2.0f);
        float textureLocalY = _clickableTexture.GetHeight() - (pixelPosition.Y + (_clickableTexture.GetHeight() / 2.0f));

        if (textureLocalX < 0 || textureLocalY < 0 ||
            textureLocalX >= image.GetSize().X || textureLocalY >= image.GetSize().Y)
        {
            return false;
        }

        var pixel = image.GetPixel((int)textureLocalX, (int)textureLocalY);
        return pixel.A > 0;
    }

    public void SetGlowColor(Color color)
    {
        MaterialOverride?.Set("shader_parameter/glow_color", color);
    }

    // Event handlers
    private void _OnClickableSpriteAreaMouseEnter(Node raycastHandler)
    {
        EmitSignal(SignalName.MouseEnter, this);
    }

    private void _OnClickableSpriteAreaMouseExit(Node raycastHandler)
    {
        EmitSignal(SignalName.MouseExit, this);
    }

    private void _OnClickableSpriteAreaMouseEnterOpaque(Node raycastHandler)
    {
        SetGlowColor(hoverColor);
        EmitSignal(SignalName.MouseEnterOpaque, this);
    }

    private void _OnClickableSpriteAreaMouseExitOpaque(Node raycastHandler)
    {
        SetGlowColor(selectableColor);
        EmitSignal(SignalName.MouseExitOpaque, this);
    }

    private void _OnClickableSpriteAreaMouseSingleClickedOpaqueArea(Node raycastHandler)
    {
        EmitSignal(SignalName.MouseLeftClickOpaque, this);
    }

    private void _OnClickableSpriteAreaMouseDoubleClickedOpaqueArea(Node raycastHandler)
    {
        EmitSignal(SignalName.MouseLeftDoubleClickOpaque, this);
    }
}
