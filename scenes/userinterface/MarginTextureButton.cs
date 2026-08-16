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
