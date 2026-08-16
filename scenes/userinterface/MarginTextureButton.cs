using Godot;

/// <summary>
/// A <see cref="TextureButton"/> wrapped in a margin so it can be dropped into a container
/// without the texture touching the container edges. Textures are stored on this node and
/// pushed down to the inner button, because exported properties are applied before the
/// scene's children exist.
/// </summary>
[Tool]
public partial class MarginTextureButton : MarginContainer
{
	private Texture2D _textureNormal;
	private Texture2D _texturePressed;

	public TextureButton TextureButton => GetNodeOrNull<TextureButton>("TextureButton");

	[Export]
	public Texture2D TextureNormal
	{
		get => _textureNormal;
		set
		{
			_textureNormal = value;
			ApplyTextures();
		}
	}

	[Export]
	public Texture2D TexturePressed
	{
		get => _texturePressed;
		set
		{
			_texturePressed = value;
			ApplyTextures();
		}
	}

	/// <summary>Disables the inner button and dims it, so an unavailable action reads as unavailable.</summary>
	public bool Disabled
	{
		get => TextureButton?.Disabled ?? false;
		set
		{
			TextureButton textureButton = TextureButton;
			if (textureButton == null)
			{
				return;
			}
			textureButton.Disabled = value;
			Modulate = value ? new Color(1, 1, 1, 0.4f) : Colors.White;
		}
	}

	public override void _Ready()
	{
		ApplyTextures();
	}

	private void ApplyTextures()
	{
		TextureButton textureButton = TextureButton;
		if (textureButton == null)
		{
			return;
		}

		// A null override means "keep whatever the scene already had".
		if (_textureNormal != null)
		{
			textureButton.TextureNormal = _textureNormal;
		}
		if (_texturePressed != null)
		{
			textureButton.TexturePressed = _texturePressed;
		}
	}
}
