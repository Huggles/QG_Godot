using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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

		// The local player's factions are not known yet when this menu enters the tree, so
		// rebuild the card backs whenever the assignment can have changed.
		EventBus.Instance.FactionsAssigned += AddFactionButtons;
		EventBus.Instance.GameSessionStarted += AddFactionButtons;
		EventBus.Instance.UserInterfaceReady += AddFactionButtons;

		AddFactionButtons();
	}

	public override void _ExitTree()
	{
		if (EventBus.Instance != null)
		{
			EventBus.Instance.FactionsAssigned -= AddFactionButtons;
			EventBus.Instance.GameSessionStarted -= AddFactionButtons;
			EventBus.Instance.UserInterfaceReady -= AddFactionButtons;
		}
	}

	private readonly List<MarginTextureButton> _factionButtons = new List<MarginTextureButton>();
	private List<Faction> _displayedFactions = new List<Faction>();

	private void AddFactionButtons()
	{
		List<Faction> localPlayerFactions = PlayerFactionRegistry.GetLocalPlayerFactions();
		if (_displayedFactions.SequenceEqual(localPlayerFactions))
		{
			return;
		}

		foreach (MarginTextureButton factionButton in _factionButtons)
		{
			BottomLeftMenuContainer.RemoveChild(factionButton);
			factionButton.QueueFree();
		}
		_factionButtons.Clear();

		foreach (Faction faction in localPlayerFactions)
		{
			FactionState factionState = FactionState.ForEnum(faction);
			Texture2D cardTexture = factionState.FactionData.CardBackTexture;

			MarginTextureButton marginTextureButton = AssetRepository.MarginTextureButtonScenePacked.Instantiate<MarginTextureButton>();
			marginTextureButton.Name = $"{faction}CardBackButton";
			marginTextureButton.TextureNormal = cardTexture;
			marginTextureButton.TexturePressed = cardTexture;

			BottomLeftMenuContainer.AddChild(marginTextureButton);
			_factionButtons.Add(marginTextureButton);
		}

		_displayedFactions = localPlayerFactions;
	}

	public void OnVisibilityButtonPressed()
	{
		VisibilityModalOpen = !VisibilityModalOpen;
		BottomLeftMenuModal.Visible = VisibilityModalOpen;
	}
}
