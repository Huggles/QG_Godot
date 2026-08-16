using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BottomLeftMenu : Control
{


	public Control BottomLeftMenuContainer => GetNode<Control>("%BottomLeftMenuHBox");
	public MarginTextureButton VisibilityButton => GetNode<MarginTextureButton>("%VisibilityButton");
	public MarginTextureButton ActiveInputRequestButton => GetNode<MarginTextureButton>("%ActiveInputRequestButton");
	public PanelContainer BottomLeftMenuModal => GetNode<PanelContainer>("%BottomLeftMenuModal");

	public Button ToggleCountryLabelsButton => GetNode<Button>("%CountryLabelsButton");
	public Button ToggleDebugMenuButton => GetNode<Button>("%DebugMenuButton");

	public bool VisibilityModalOpen = false;

	/// <summary>The faction whose hand this menu put on the display, or NONE when it did not.</summary>
	private Faction _browsingFaction = Faction.NONE;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		BottomLeftMenuModal.Visible = false;
		VisibilityButton.TextureButton.Pressed += OnVisibilityButtonPressed;
		ActiveInputRequestButton.TextureButton.Pressed += OnActiveInputRequestButtonPressed;

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

		EventBus.Instance.CardPromptOpened += OnCardPromptOpened;
		EventBus.Instance.CardPromptClosed += OnCardPromptClosed;

		AddFactionButtons();
		RefreshActiveInputRequestButton();
	}

	public override void _ExitTree()
	{
		if (EventBus.Instance != null)
		{
			EventBus.Instance.FactionsAssigned -= AddFactionButtons;
			EventBus.Instance.GameSessionStarted -= AddFactionButtons;
			EventBus.Instance.UserInterfaceReady -= AddFactionButtons;
			EventBus.Instance.CardPromptOpened -= OnCardPromptOpened;
			EventBus.Instance.CardPromptClosed -= OnCardPromptClosed;
		}
	}

	private readonly Dictionary<Faction, MarginTextureButton> _factionButtons = new Dictionary<Faction, MarginTextureButton>();
	private List<Faction> _displayedFactions = new List<Faction>();

	private void AddFactionButtons()
	{
		List<Faction> localPlayerFactions = PlayerFactionRegistry.GetLocalPlayerFactions();
		if (_displayedFactions.SequenceEqual(localPlayerFactions))
		{
			return;
		}

		// Faction assignment can land before the game state does. Leave the buttons for a later
		// event rather than building them off a half-populated state.
		if (localPlayerFactions.Any(faction => FactionState.ForEnum(faction) == null))
		{
			return;
		}

		foreach (MarginTextureButton factionButton in _factionButtons.Values)
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
			marginTextureButton.TooltipText = $"Show the {faction} hand";

			BottomLeftMenuContainer.AddChild(marginTextureButton);
			// Captured rather than read back off the sender: the button carries no faction of its own.
			marginTextureButton.TextureButton.Pressed += () => OnFactionButtonPressed(faction);
			_factionButtons[faction] = marginTextureButton;
		}

		_displayedFactions = localPlayerFactions;
	}

	/// <summary>
	/// Puts the faction's hand on the shared <see cref="FactionHandDisplay"/>, or takes it back off
	/// when that faction is already the one being browsed.
	///
	/// Nothing browsed this way is selectable — an empty selectable set is passed explicitly. The
	/// display is the same node an open card prompt draws into and it keeps the prompt's CardSelected
	/// subscription alive while browsing, so a clickable card here would answer that prompt with a
	/// card the host never offered.
	/// </summary>
	private void OnFactionButtonPressed(Faction faction)
	{
		if (FactionHandDisplay.Current == null)
		{
			return;
		}

		// Guarded on FactionState rather than on DeckState: DeckState.ForFaction dereferences the
		// FactionState, which is null until the game state has arrived.
		FactionState factionState = FactionState.ForEnum(faction);
		DeckState deckState = factionState?.DeckState;

		if (_browsingFaction == faction)
		{	
			return;
		}
		if(InputManager.CurrentCardPrompt != null)
		{
			if (faction == InputManager.CurrentCardPrompt.Faction && InputManager.CurrentCardPrompt.DisplayCardIds.Except(factionState.DeckState.HandCardIds).ToList().Count == 0)
			{
				CloseBrowsing();
				return;
			}
		}
		
		if (deckState == null)
		{
			return;
		}

		_browsingFaction = faction;
		FactionHandDisplay.Current.Show(deckState.HandCardIds, faction, new List<int>());
	}

	/// <summary>
	/// Leaves browsing mode: hands the display back to the open card prompt if there is one, so a
	/// player who looked away mid-prompt gets their actual choices back rather than a dead hand.
	/// </summary>
	private void CloseBrowsing()
	{
		_browsingFaction = Faction.NONE;
		if (!InputManager.ShowCurrentCardPrompt())
		{
			FactionHandDisplay.Current.Hide();
		}
	}

	private void OnActiveInputRequestButtonPressed()
	{
		_browsingFaction = Faction.NONE;
		InputManager.ShowCurrentCardPrompt();
	}

	private void OnCardPromptOpened(int faction)
	{
		// The prompt drew itself over whatever was being browsed.
		_browsingFaction = Faction.NONE;
		RefreshActiveInputRequestButton();
	}

	private void OnCardPromptClosed()
	{
		RefreshActiveInputRequestButton();
	}

	private void RefreshActiveInputRequestButton()
	{
		bool promptOpen = InputManager.CurrentCardPrompt != null;
		ActiveInputRequestButton.Disabled = !promptOpen;
		ActiveInputRequestButton.TooltipText = promptOpen
			? "Show the cards you are being asked to choose from"
			: "No card choice is open";
	}

	public void OnVisibilityButtonPressed()
	{
		VisibilityModalOpen = !VisibilityModalOpen;
		BottomLeftMenuModal.Visible = VisibilityModalOpen;
	}
}
