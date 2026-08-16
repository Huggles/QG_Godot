#if TOOLS
using Godot;
using System;

[Tool]
public partial class MarginTextureButton : MarginContainer
{
	
	[Export]
	public Texture2D TextureNormal {
		get { return TextureButton.TextureNormal; }
		set { if(this.TextureButton != null ) TextureButton.TextureNormal = value; }
	}

	[Export]
	public Texture2D TexturePressed {
		get { return TextureButton.TexturePressed; }
		set { if(this.TextureButton != null ) TextureButton.TexturePressed = value; }
	}
}

#endif

public partial class MarginTextureButton : MarginContainer
{
	public TextureButton TextureButton => GetNodeOrNull<TextureButton>("TextureButton");
}
