using Godot;
using System;
using System.Collections.Generic;

public partial class BottomLeftMenu : Control
{

	
	public Control BottomLeftMenuContainer => GetNode<Control>("%BottomLeftMenuHBox");
	public MarginTextureButton VisibilityButton => GetNode<MarginTextureButton>("%VisibilityButton");
	public PanelContainer BottomLeftMenuModal => GetNode<PanelContainer>("%BottomLeftMenuModal");

	public Button ToggleCountryLabelsButton => GetNode<Button>("%CountryLabelsButton");
	public Button ToggleDebugMenuButton => GetNode<Button>("%DebugMenuButton");

	public bool VisibilityModalOpen = false;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		BottomLeftMenuModal.Visible = false;
		VisibilityButton.TextureButton.Pressed += OnVisibilityButtonPressed;

		ToggleCountryLabelsButton.ButtonPressed = GameSettings.ShowCountryLabels;
		ToggleCountryLabelsButton.Pressed += () => {
			EventBus.Instance.EmitSignal(EventBus.SignalName.CountryNamesToggled, ToggleCountryLabelsButton.ButtonPressed);			
			GameSettings.Instance.SetShowCountryLabels(ToggleCountryLabelsButton.ButtonPressed);
		};

		ToggleDebugMenuButton.ButtonPressed = GameSettings.ShowDebugMenu;
		ToggleDebugMenuButton.Pressed += () => {
			EventBus.Instance.EmitSignal(EventBus.SignalName.DebugMenuToggled, ToggleDebugMenuButton.ButtonPressed);
			GameSettings.Instance.SetShowDebugMenu(ToggleDebugMenuButton.ButtonPressed);
		};

		AddFactionButtons();
	}

	private List<MarginTextureButton> _factionButtons = new List<MarginTextureButton>();
	private void AddFactionButtons()
	{
		List<Faction> localPlayerFactions = PlayerFactionRegistry.GetLocalPlayerFactions();
		if(_factionButtons.Count == 0 || localPlayerFactions.Count != _factionButtons.Count)
		{
			_factionButtons.ForEach(button => button.QueueFree());
			foreach (var faction in localPlayerFactions)
			{
				FactionState factionState = FactionState.ForEnum(faction);
				Texture2D cardTexture = factionState.FactionData.CardBackTexture;
				MarginTextureButton marginTextureButton = new MarginTextureButton();				
				_factionButtons.Add(marginTextureButton);
				BottomLeftMenuContainer.Ready += () => {
					marginTextureButton.TextureButton.TextureNormal = cardTexture;	
					marginTextureButton.TextureButton.TexturePressed = cardTexture;		
				};
				BottomLeftMenuContainer.AddChild(marginTextureButton);			
			}
		}
	}

	public void OnVisibilityButtonPressed()
	{
		VisibilityModalOpen = !VisibilityModalOpen;
		BottomLeftMenuModal.Visible = VisibilityModalOpen;
	}
}
