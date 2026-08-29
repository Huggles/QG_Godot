using Godot;
using System;

public partial class WorldScene : Control
{
	// Called when the node enters the scene tree for the first time.


	public WorldPresentationMode worldPresentationMode
	{
		get {
			return field;
		}
		set {
			field = value;            
			ChangeWorldPresentationMode(value);
		}
	}

	public TextureRect WorldSprite => GetNode<TextureRect>("%WorldSprite");

	public override void _Ready()
	{
		EventBus.Instance.WorldPresentationViewChanged += OnWorldPresentationViewChanged;
	}

	public override void _ExitTree()
	{
		EventBus.Instance.WorldPresentationViewChanged -= OnWorldPresentationViewChanged;
	}

	private void OnWorldPresentationViewChanged(WorldPresentationMode mode)
	{
		worldPresentationMode = mode;
	}

	private void ChangeWorldPresentationMode(WorldPresentationMode newMode = WorldPresentationMode.Normal)
	{
		switch (newMode)
		{
			case WorldPresentationMode.Normal:
				ShowWorldPresentationNormal();
				break;
			case WorldPresentationMode.Tactical:
			// Both tactical views hide the ordinary board and let the per-country overlays draw it;
			// they differ only in the palette CountryScene paints, not in what the world shows.
			case WorldPresentationMode.TacticalTeam:
				ShowWorldPresentationTactical();
				break;
		}        
	}

	private void ShowWorldPresentationNormal()
	{
		WorldSprite.Visible = true; 
	}

	private void ShowWorldPresentationTactical()
	{
		WorldSprite.Visible = false;
	}
}
