using Godot;
using System;

public partial class ClickableSprite : Area2D
{
	public Sprite2D Sprite => GetNode<Sprite2D>("Sprite2D");
	private CollisionShape2D CollisionShape => GetNode<CollisionShape2D>("CollisionShape2D");

	private bool IsClickable = false;
	private Tween _alphaWaveTween;

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
	


	public static Color HOVER_COLOR = new Color(0.5f, 1, 0.5f, .8f);
    public static Color SELECTABLE_COLOR = new Color(1,1,1,.8f);

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
		// Assign before the tween: a sprite left faint by a previous subdued stint would otherwise stay
		// faint for most of the 20-second leg it takes the wave to climb back.
		Sprite.Modulate = _restingColor;
		IsClickable = true;
		CollisionShape.Disabled = false;
		CollisionShape.Visible = true;
		SpriteAlphaWaveAnimation();
	}

	/// <summary>
	/// Clickable, but drawn as a secondary target: a flat faint alpha with NO pulse. Used for the
	/// rebuild-in-place deploy target on a unit, a rare option offered beside the ordinary ones that
	/// must not compete with them for attention.
	///
	/// Deliberately not a fainter version of the wave. One leg of that wave lasts
	/// DurationLongSeconds * 10 — twenty seconds at Normal speed — so a subdued target starting from
	/// SELECTABLE_COLOR's 0.8 spent most of the selection at ordinary opacity and read as an ordinary
	/// target. Holding still is also the clearer signal: the ordinary targets are the ones that
	/// breathe. Hover still brightens to HOVER_COLOR, so the sprite is unambiguous under the mouse.
	/// </summary>
	public void SetClickableSubdued()
	{
		_restingColor = SUBDUED_COLOR;
		StopAlphaWaveAnimation();
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
		StopAlphaWaveAnimation();
	}

	public void SpriteAlphaWaveAnimation()
	{
		_alphaWaveTween?.Kill();
		_alphaWaveTween = GetTree().CreateTween().SetLoops();
		_alphaWaveTween.TweenProperty(Sprite, "modulate:a", 0.3, GameSettings.DurationLongSeconds * 10);
		_alphaWaveTween.TweenProperty(Sprite, "modulate:a", 0.8, GameSettings.DurationLongSeconds * 10);
	}

	public void StopAlphaWaveAnimation()
	{
		_alphaWaveTween?.Kill();
		_alphaWaveTween = null;
	}


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
