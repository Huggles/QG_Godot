using Godot;
using System;

public partial class ClickableSprite : Area2D
{
	public Sprite2D Sprite => GetNode<Sprite2D>("Sprite2D");
	private CollisionShape2D CollisionShape => GetNode<CollisionShape2D>("CollisionShape2D");

	private bool IsClickable = false;

	/// <summary>This marker's own copy of the radar shader. See <see cref="TargetRadar.CreateMaterial"/>.</summary>
	private ShaderMaterial _radarMaterial;

	[Export]
	private Texture2D texture;

	private Image _Image;
	public Image Image
	{
		get
		{
			if (_Image == null)
			{
				_Image = texture.GetImage();
				if (_Image != null && Image.IsCompressed())
				{
					_Image.Decompress();
				}
			}
			return _Image;
		}
	}

	public Vector2 Size => new Vector2(this.texture.GetWidth(), this.texture.GetHeight()) * this.Scale;
	public Rect2 Bounds => new Rect2(this.Position - (Size/2), Size);
	


	public static Color HOVER_COLOR = new Color(0.5f, 1, 0.5f, 1);

	/// <summary>
	/// Resting colour of an ordinary offered target. Fully opaque, because the alpha it is actually
	/// drawn at comes from TargetRadarSweep.gdshader — a faint icon with an opaque slice sweeping over
	/// it — which multiplies into this.
	/// </summary>
    public static Color SELECTABLE_COLOR = new Color(1,1,1,1);

	/// <summary>
	/// Resting colour for a secondary target — one offered alongside the ordinary ones for a rarer
	/// action. Fainter so it reads as the unusual option without disappearing. See
	/// <see cref="SetClickableSubdued"/>.
	/// </summary>
	public static Color SUBDUED_COLOR = new Color(1,1,1,.3f);

	public bool mouseOverOpaque = false;

	/// <summary>Alpha the sprite returns to when the mouse leaves. Lowered by the subdued mode.</summary>
	private Color _restingColor = SELECTABLE_COLOR;

	[Signal] public delegate void MouseLeftClickOnOpaqueEventHandler();
	[Signal] public delegate void MouseRightClickOnOpaqueEventHandler();
	[Signal] public delegate void MouseEnterOpaqueEventHandler();
	[Signal] public delegate void MouseExitOpaqueEventHandler();

	public override void _Ready()
	{
		// On the Sprite2D, not on this Area2D: the Area2D draws nothing, and a CanvasItem material does
		// not carry down to children unless they ask for it with use_parent_material.
		_radarMaterial = TargetRadar.CreateMaterial();
		Sprite.Material = _radarMaterial;

		Sprite.Modulate = SELECTABLE_COLOR;
		InputEvent += OnInputEvent;
		MouseEntered += OnMouseEntered;
		MouseExited += OnMouseExited;		
		MouseEnterOpaque += OnMouseEnterSpriteOpaque;
        MouseExitOpaque += OnMouseExitSpriteOpaque;
    }

	public void ShowSprite()
	{
		Visible = true;
		Sprite.Visible = true;
	}

	public void HideSprite()
	{
		Visible = false;
		Sprite.Visible = false;
	}

	public void SetClickable()
	{
		_restingColor = SELECTABLE_COLOR;
		Sprite.Modulate = _restingColor;
		IsClickable = true;
		CollisionShape.Disabled = false;
		CollisionShape.Visible = true;
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
	/// brightens to HOVER_COLOR, so the sprite is unambiguous under the mouse.
	/// </summary>
	public void SetClickableSubdued()
	{
		_restingColor = SUBDUED_COLOR;
		StopRadarSweep();
		Sprite.Modulate = _restingColor;
		IsClickable = true;
		CollisionShape.Disabled = false;
		CollisionShape.Visible = true;
	}

	public void SetUnclickable()
	{
		_restingColor = SELECTABLE_COLOR;
		IsClickable = false;
		CollisionShape.Disabled = true;
		CollisionShape.Visible = false;
		StopRadarSweep();
	}

	public void StartRadarSweep() => TargetRadar.Start(_radarMaterial);

	public void StopRadarSweep() => TargetRadar.Stop(_radarMaterial);


	private void OnMouseEnterSpriteOpaque()
	{
		DebugUtilities.PrintPeerFinest("OnMouseEnterSpriteOpaque");
		Sprite.Modulate = HOVER_COLOR;
	}

    private void OnMouseExitSpriteOpaque()
    {
        DebugUtilities.PrintPeerFinest("OnMouseExitSpriteOpaque");
        // The resting colour, not SELECTABLE_COLOR: a subdued target that has been hovered once must
        // fall back to being subdued, otherwise it is left as prominent as an ordinary target.
        Sprite.Modulate = _restingColor;
    }


    private void OnMouseEntered()
	{
	}

	private void OnMouseExited()
	{
		if (mouseOverOpaque == true)
		{
			mouseOverOpaque = false;
			EmitSignal(SignalName.MouseExitOpaque);
		}
    }

	public override void _Process(double delta)
	{
		if (IsClickable == false)
			return;
		if (IsMouseOverSprite() == false)
			return;
		HandleMouseOverMovement();
	}

	private bool IsMouseOverSprite()
	{		
		Vector2 mousePos = GetGlobalMousePosition();
		Vector2 localMousePos = ToLocal(mousePos);
		if (CollisionShape.Shape is RectangleShape2D rectShape)
		{
			var rect = new Rect2(-rectShape.Size / 2, rectShape.Size);
			if (rect.HasPoint(localMousePos))
			{
				return true;
			}
		}
		return false;
	}

	private void OnInputEvent(Node viewport, InputEvent inputEvent, long shapeIdx)
	{
		// if (inputEvent is InputEventMouseMotion)
		// {
		// 	HandleMouseOverMovement();
		// }
		if (inputEvent is InputEventMouseButton)
		{
			HandleMouseButtonClick(viewport, inputEvent as InputEventMouseButton, shapeIdx);
		}

	}
	
	private void HandleMouseOverMovement()
	{
		Vector2 mousePos = GetGlobalMousePosition();
		if (GetNode<CollisionShape2D>("CollisionShape2D").Shape is RectangleShape2D shape)
		{
			var rect = new Rect2(-shape.Size / 2, shape.Size);
			Vector2 localPos = ToLocal(mousePos);
			localPos += (rect.Size / 2);
			Color color = Image.GetPixel((int)localPos.X, (int)localPos.Y);
			if (color.A > 0)
			{
				if (mouseOverOpaque == false)
				{
					EmitSignal(SignalName.MouseEnterOpaque);
				}
				mouseOverOpaque = true;				
			}
			else if (mouseOverOpaque == true)
			{
				mouseOverOpaque = false;
				EmitSignal(SignalName.MouseExitOpaque);
			}
		}
	}

	private void HandleMouseButtonClick(Node viewport, InputEventMouseButton inputEvent, long shapeIdx)
	{
		if (mouseOverOpaque)
		{
			if (inputEvent.ButtonIndex == MouseButton.Left)
			{
				EmitSignal(SignalName.MouseLeftClickOnOpaque);
			}
			else if (inputEvent.ButtonIndex == MouseButton.Right)
			{
				EmitSignal(SignalName.MouseLeftClickOnOpaque);
			}
			
		}
	}

	public void SetTexture(Texture2D texture)
	{
		this.texture = texture;
		Sprite.Texture = texture;

		Vector2 textureSize = Sprite.Texture.GetSize();
		RectangleShape2D rectShape = new RectangleShape2D();
		rectShape.Size = textureSize;
		CollisionShape.Shape = rectShape;
	}
}
