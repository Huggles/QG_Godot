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
	public Button WorldPresentationButton => GetNode<Button>("%WorldPresentationButton");

	public bool VisibilityModalOpen = false;

	/// <summary>
	/// The faction whose hand this menu put on the display, or NONE when it did not.
	///
	/// A property rather than a field so every one of the four places that ends browsing keeps
	/// <see cref="FactionFocus"/> in step — browsing is claimed over a live input request rather than
	/// instead of one, so dropping the claim is what hands the tint back to the prompt underneath.
	/// </summary>
	private Faction BrowsingFaction
	{
		get => _browsingFaction;
		set
		{
			_browsingFaction = value;
			FactionFocus.Set(FactionFocusSource.Browsing, value);
		}
	}
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

		WorldPresentationButton.Pressed += CycleWorldPresentationMode;
		RefreshWorldPresentationButton();

		// The local player's factions are not known yet when this menu enters the tree, so
		// rebuild the card backs whenever the assignment can have changed.
		EventBus.Instance.FactionsAssigned += AddFactionButtons;
		EventBus.Instance.GameSessionStarted += AddFactionButtons;
		EventBus.Instance.UserInterfaceReady += AddFactionButtons;

		EventBus.Instance.CardPromptOpened += OnCardPromptOpened;
		EventBus.Instance.CardPromptClosed += OnCardPromptClosed;
		EventBus.Instance.RecallablePromptChanged += RefreshActiveInputRequestButton;

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
			EventBus.Instance.RecallablePromptChanged -= RefreshActiveInputRequestButton;
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
		// Hoisted above the toggle branches, which both read the hand: the second one dereferenced
		// factionState directly and would NRE before the game state arrived.
		if (deckState == null)
		{
			return;
		}

		// Pressing the card back of the hand already on the display puts it away again. This used to bail
		// out instead of toggling, which left the button dead on the second press — and toggling off is now
		// how a player gets back a prompt that browsing auto-parked.
		if (BrowsingFaction == faction)
		{
			CloseBrowsing();
			return;
		}

		// The prompt this peer is being asked right now already covers this hand, so browse would only
		// replace live choices with a dead copy of them. Show the prompt instead.
		if (PromptCoversHand(faction, deckState))
		{
			CloseBrowsing();
			return;
		}

		// FactionHandDisplay sits below the modal band, so a hand browsed while a prompt modal is up would
		// be drawn behind it. Put the prompt aside instead — the recall button brings it back, and parking
		// never answers it.
		ModalStack.Current?.ParkTopRequest();

		BrowsingFaction = faction;
		FactionHandDisplay.Current.Show(deckState.HandCardIds, faction, new List<int>());
	}

	/// <summary>
	/// Whether the card prompt open on this peer is already a view of this faction's hand — in which case
	/// pressing the card back must hand that prompt back rather than browse over it, since browsing draws
	/// the same cards with nothing selectable.
	///
	/// <see cref="InputManager.CurrentCardPrompt"/> being non-null is what makes this "the request is for
	/// this instance": the prompt is only ever opened from a request's Handle(), which runs on the target
	/// peer alone, and it is cleared the moment the request is answered.
	/// </summary>
	private static bool PromptCoversHand(Faction faction, DeckState deckState)
	{
		ActiveCardPrompt prompt = InputManager.CurrentCardPrompt;
		if (prompt == null || prompt.Faction != faction)
		{
			return false;
		}

		// The hand-play prompt is the hand, plus the table cards that can be activated instead of playing
		// one — so it covers the hand even though its display set is deliberately wider than it. The
		// card-id comparison below cannot see that, which is why the request says so itself.
		return prompt.IsHandPlay
			|| !prompt.DisplayCardIds.Except(deckState.HandCardIds).Any();
	}

	/// <summary>
	/// Leaves browsing mode: hands the display back to the open card prompt if there is one, so a
	/// player who looked away mid-prompt gets their actual choices back rather than a dead hand.
	/// Failing that, gives back a modal prompt that browsing auto-parked to make room for itself.
	/// </summary>
	private void CloseBrowsing()
	{
		BrowsingFaction = Faction.NONE;
		if (InputManager.ShowCurrentCardPrompt())
		{
			return;
		}

		FactionHandDisplay.Current.Hide();
		ModalStack.Current?.Recall();
	}

	private void OnActiveInputRequestButtonPressed()
	{
		BrowsingFaction = Faction.NONE;
		RecallablePrompts.Recall();
	}

	private void OnCardPromptOpened(int faction)
	{
		// The prompt drew itself over whatever was being browsed.
		BrowsingFaction = Faction.NONE;
		RefreshActiveInputRequestButton();
	}

	private void OnCardPromptClosed()
	{
		RefreshActiveInputRequestButton();
	}

	/// <summary>
	/// One button for every kind of prompt that can be brought back — an open card prompt drawn over by
	/// browsing, or a modal prompt the player put aside. <see cref="RecallablePrompts"/> is the single
	/// slot both register with, so this does not grow a branch per prompt kind.
	/// </summary>
	private void RefreshActiveInputRequestButton()
	{
		IRecallablePrompt prompt = RecallablePrompts.Current;
		ActiveInputRequestButton.Disabled = prompt == null;
		ActiveInputRequestButton.TooltipText = prompt?.RecallTooltip ?? "Nothing to bring back";
	}

	public void OnVisibilityButtonPressed()
	{
		VisibilityModalOpen = !VisibilityModalOpen;
		BottomLeftMenuModal.Visible = VisibilityModalOpen;
	}

	/// <summary>
	/// The view the map is currently drawing. Held here rather than read back off a CountryScene because
	/// this button is what decides it: every country follows the signal, so the menu is the one place the
	/// current value exists. Session-only — unlike its two neighbours in this modal it is not persisted,
	/// so the map always opens in Normal.
	/// </summary>
	private WorldPresentationMode _worldPresentationMode = WorldPresentationMode.Normal;

	/// <summary>
	/// Steps to the next <see cref="WorldPresentationMode"/> and tells the map. Walks Enum.GetValues and
	/// wraps rather than branching Normal/Tactical, so a third view added to the enum joins the rotation
	/// with no change here — which is also why this is a plain button and not a two-state toggle.
	/// </summary>
	private void CycleWorldPresentationMode()
	{
		WorldPresentationMode[] modes = Enum.GetValues<WorldPresentationMode>();
		int next = (Array.IndexOf(modes, _worldPresentationMode) + 1) % modes.Length;
		_worldPresentationMode = modes[next];

		EventBus.Emit(EventBus.SignalName.WorldPresentationViewChanged, (int)_worldPresentationMode);
		RefreshWorldPresentationButton();
	}

	/// <summary>
	/// Labels the button with the view currently on screen, not the one the next press would bring up:
	/// the other rows in this modal read as state rather than as actions, and a row that named its own
	/// destination would disagree with them.
	/// </summary>
	private void RefreshWorldPresentationButton()
	{
		WorldPresentationButton.Text = $"View: {_worldPresentationMode}";
	}
}
