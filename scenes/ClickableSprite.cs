using Godot;
using System;

public partial class ClickableSprite : Area2D
{
	public Sprite2D Sprite => GetNode<Sprite2D>("Sprite2D");
	private CollisionShape2D CollisionShape => GetNode<CollisionShape2D>("CollisionShape2D");

	private Texture2D texture;
	private Image image;
	private bool mouseOverOpaque;

	[Signal] public delegate void MouseLeftClickOnOpaqueEventHandler();
	[Signal] public delegate void MouseRightClickOnOpaqueEventHandler();
	[Signal] public delegate void MouseEnterOpaqueEventHandler();
	[Signal] public delegate void MouseExitOpaqueEventHandler();

	public override void _Ready()
	{
		InputEvent += OnInputEvent;
		MouseEntered += OnMouseEntered;
		MouseExited += OnMouseExited;
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

	private void OnInputEvent(Node viewport, InputEvent inputEvent, long shapeIdx)
	{
		if (inputEvent is InputEventMouseMotion)
		{
			handleMouseOverMovement(viewport, inputEvent as InputEventMouseMotion, shapeIdx);
		}
		if (inputEvent is InputEventMouseButton)
		{
			handleMouseButtonClick(viewport, inputEvent as InputEventMouseButton, shapeIdx);
		}
		
	}
	
	private void handleMouseOverMovement(Node viewport, InputEventMouseMotion inputEvent, long shapeIdx)
	{
		var mousePos = GetGlobalMousePosition();
		if (GetNode<CollisionShape2D>("CollisionShape2D").Shape is RectangleShape2D shape)
		{
			var rect = new Rect2(-shape.Size / 2, shape.Size);
			Vector2 localPos = ToLocal(mousePos);
			localPos += (rect.Size / 2);
			Color color = image.GetPixel((int)localPos.X, (int)localPos.Y);
			if (color.A > 0)
			{
				mouseOverOpaque = true;
				EmitSignal(SignalName.MouseEnterOpaque);
			}
			else if (mouseOverOpaque == true)
			{
				mouseOverOpaque = false;
				EmitSignal(SignalName.MouseExitOpaque);
			}
		}
	}

	private void handleMouseButtonClick(Node viewport, InputEventMouseButton inputEvent, long shapeIdx)
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
		this.image = texture.GetImage();
		if (this.image != null && this.image.IsCompressed())
			this.image.Decompress();
		Sprite.Texture = texture;

		Vector2 textureSize = Sprite.Texture.GetSize();
		RectangleShape2D rectShape = new RectangleShape2D();
		rectShape.Size = textureSize;

		CollisionShape.Shape = rectShape;
	}
}
