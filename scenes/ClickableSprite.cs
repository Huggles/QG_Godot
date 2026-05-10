using Godot;
using System;

public partial class ClickableSprite : Area2D
{
	public Sprite2D Sprite => GetNode<Sprite2D>("Sprite2D");
	private CollisionShape2D CollisionShape => GetNode<CollisionShape2D>("CollisionShape2D");

	private bool IsClickable = false;	

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
	


	public static Color HOVER_COLOR = new Color(0.5f, 1, 0.5f, .8f);
    public static Color SELECTABLE_COLOR = new Color(1,1,1,.8f);
	public bool mouseOverOpaque = false;

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
		IsClickable = true;
		CollisionShape.Disabled = false;
		CollisionShape.Visible = true;
		SpriteAlphaWaveAnimation();
	}

	public void SetUnclickable()
	{
		IsClickable = false;
		CollisionShape.Disabled = true;
		CollisionShape.Visible = false;
	}

	public void SpriteAlphaWaveAnimation()
    {
        var tween1 = GetTree().CreateTween();        
        PropertyTweener propertyTweener1 = tween1.TweenProperty(Sprite, "modulate:a", 0.3, GameSettings.AnimationDuration * 10);
        propertyTweener1.Finished += () =>
        {
			tween1.Dispose();
			var tween2 = GetTree().CreateTween();
            PropertyTweener propertyTweener2 = tween2.TweenProperty(Sprite, "modulate:a", 0.8, GameSettings.AnimationDuration * 10);
            propertyTweener2.Finished += () =>
            {
				tween2.Dispose();
                if (Sprite.Visible)
				{
					SpriteAlphaWaveAnimation();
				}
            };
        };        
    }


	private void OnMouseEnterSpriteOpaque()
	{
		DebugUtilities.PrintPeer("OnMouseEnterSpriteOpaque");
		Sprite.Modulate = HOVER_COLOR;
	}

    private void OnMouseExitSpriteOpaque()
    {
        DebugUtilities.PrintPeer("OnMouseExitSpriteOpaque");
        Sprite.Modulate = SELECTABLE_COLOR;
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
