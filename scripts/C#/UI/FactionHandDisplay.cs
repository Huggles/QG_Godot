using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class FactionHandDisplay : Control
{

	public static FactionHandDisplay Current;

	private Control CardsContainer => GetNode<Panel>("%CardsContainerPanel");
	private Panel CardPreviewContainer => GetNode<Panel>("%CardPreviewContainer");
	private CardScene CardPreview => GetNode<CardScene>("%CardPreview");
	private Button TestButton => GetNode<Button>("%TestButton");

	private List<CardScene> CardScenes = new List<CardScene>();
	private Faction showingFaction;

	[Signal] public delegate void CardSelectedEventHandler(int cardId);

	// Event handlers for cleanup
	private Action onTestButtonPressed;

	


	public override void _Ready()
	{        
		if(GetMultiplayerAuthority() == Multiplayer.GetUniqueId())
		{
			Current = this;
			LoadUI();          
		}        
	}  


	public override void _ExitTree()
	{
		UnsubscribeFromEvents();
	}

	private void LoadUI()
	{
		Hide();        
		EventBus.Instance.GameSessionStarted += OnGameSessionStarted;
		EventBus.Instance.NextStepStarted += OnNextStepStarted;
		EventBus.Instance.CardsDrawn += OnCardsDrawn;
		EventBus.Instance.CardsDiscarded += OnCardsDiscarded;

		// Unsubscribe first to prevent duplicate connections
		UnsubscribeFromEvents();

		onTestButtonPressed = () =>
		{
			if (!IsInstanceValid(this) || !IsInsideTree()) return;
			
			PresentationModal.Current.ShowModalPersistent(PresentationItemImageButton.ForFactions([Faction.GERMANY,Faction.JAPAN]), "Select a faction");
		};
		TestButton.Pressed += onTestButtonPressed;
	}

	private void OnCardsDrawn(int faction, int numberOfCards)
	{
		if (showingFaction == (Faction)faction)
		{
			Show(showingFaction);
		}
	}

	private void OnCardsDiscarded(int faction, int numberOfCards)
	{
		if (showingFaction == (Faction)faction)
		{
			Show(showingFaction);
		}
	}

	private void OnGameSessionStarted()
	{
		DebugUtilities.PrintPeerFinest($"Game session started, showing hand display for first faction: {PlayerScene.Current.ControlledFactions[0]}");
		Show(PlayerScene.Current.ControlledFactions[0]);
	}


	private void OnNextStepStarted(int turnStep)
	{
		switch((TurnStep)turnStep)
		{
			case TurnStep.START:       
				OnStartTurnStepStarted();
				break;
			case TurnStep.PLAY_CARD:            
				OnPlayCardTurnStepStarted();
				break;
			default:
				Hide();
				break;
		};
	}

	private void OnStartTurnStepStarted()
	{
		
	}

	private void OnPlayCardTurnStepStarted()
	{        
		if(PlayerScene.Current.ControlledFactions.Contains(GameFlow.Instance.CurrentFaction))
		{
			Show(GameFlow.Instance.CurrentFaction);
		}
	}

	public void Show(Faction faction)
	{
		showingFaction = faction;
		ResetVisibility();
		if (faction != Faction.NONE && faction != Faction.ALL)
		{
			InitCards(DeckState.ForFaction(faction).HandCardIds);
		}
	}

	public void Show(List<int> cardIds)
	{
		Show(cardIds, cardIds.Count > 0 ? CardState.ForId(cardIds[0]).Faction : Faction.NONE); // Assumes all cards are from the same faction, which should be true for hand display
	}

	/// <param name="selectableCardIds">
	/// Which of <paramref name="cardIds"/> may actually be clicked. Null falls back to
	/// <see cref="Tag.IsActivatable"/>, which is what every non-prompt caller wants.
	///
	/// A reaction prompt must pass it: it draws the faction's whole event-triggered table so the
	/// player can see why nothing applies, and only the host knows which of those are playable —
	/// block-eligible cards in particular, since Tag.IsBlockReaction is never replicated.
	/// </param>
	public void Show(List<int> cardIds, Faction faction, List<int> selectableCardIds = null)
	{
		showingFaction = faction;
		ResetVisibility();
		InitCards(cardIds, selectableCardIds);
	}

	private void ResetVisibility()
	{
		Visible = true;
		
		// Only access CardsContainer if it's been initialized (in LoadUI)
		if (CardsContainer != null)
		{
			CardsContainer.MouseFilter = MouseFilterEnum.Stop;
		}
		
		HideCardEmphasis();
	}

	public new void Hide()
	{
		Visible = false;
		// The trigger context is no longer a child of this node and is deliberately NOT cleared here:
		// a mutator's Bulletin has to outlive the hand display, which every step transition hides.
		// InputManager.HandleItemSelected clears it for card prompts, InputRequest.Execute for the rest.

		// Only access CardsContainer if it's been initialized (in LoadUI)
		if (CardsContainer != null)
		{
			CardsContainer.MouseFilter = MouseFilterEnum.Ignore;
			DeleteCurrentCards();
		}
	}

	private void InitCards(List<int> cardIds, List<int> selectableCardIds = null)
	{
		// Only initialize cards if LoadUI has been called
		DebugUtilities.PrintPeerFinest($"Initializing cards {string.Join(", ", cardIds)}");        
		if (CardsContainer == null)
		{
			return;
		}
		
		DeleteCurrentCards();

		const int cardStepSize = 100;
		const int rotationStepSize = 10;
		Vector2 cardSize = new Vector2(500, 700);
		Vector2 cardScale = new Vector2(0.5f, 0.5f);

		float totalRotationSize = (cardIds.Count - 1) * rotationStepSize;
		float totalSizeX = (cardIds.Count - 1) * cardStepSize;

		foreach (var (cardId, index) in cardIds.Select((cardId, index) => (cardId, index)))
		{
			CardState cardState = CardState.ForId(cardId);
			var cardSceneInstance = CardScene.CardScenePackedPath.Instantiate<CardScene>();
			cardSceneInstance.Size = cardSize;
			cardSceneInstance.Scale = cardScale;
			CardsContainer.AddChild(cardSceneInstance);

			Vector2 basePosition = new Vector2(CardsContainer.Size.X * 0.5f, 0);
			basePosition.X += cardStepSize * index;
			basePosition.X -= (totalSizeX / 2) + cardSceneInstance.PivotOffset.X;
			basePosition.Y -= cardSceneInstance.Size.Y / 3;


			cardSceneInstance.Position = basePosition;
			cardSceneInstance.ZIndex = index;
			cardSceneInstance.RotationDegrees = index * rotationStepSize - (totalRotationSize / 2f);

			cardSceneInstance.Selected += OnCardSelected;
			CardScenes.Add(cardSceneInstance);
			cardSceneInstance.ShowCard(cardId);

			// The caller's list wins when it gave one. Tag.IsActivatable is the right default for the
			// plain hand display, but it is the wrong answer inside a prompt: block-eligible cards
			// carry Tag.IsBlockReaction, which is server-internal and never reaches a client, so a
			// card the host is offering would render greyed out and refuse the click.
			bool selectable = selectableCardIds?.Contains(cardId)
							  ?? cardState.HasTag(Tag.IsActivatable, cardState.Faction);
			cardSceneInstance.SetClickable(selectable);
			cardSceneInstance.SetActivatable(selectable);
		}
	}

	private void OnCardSelected(int cardId)
	{
		DebugUtilities.PrintPeer($"Card selected with ID: {cardId}");
		EmitSignal(SignalName.CardSelected, cardId);
	}

	private void DeleteCurrentCards()
	{
		foreach (CardScene cardScene in CardScenes)
		{
			// Only remove from CardsContainer if it's been initialized
			if (CardsContainer != null)
			{
				CardsContainer.RemoveChild(cardScene);
			}
			cardScene.Selected -= OnCardSelected;
			cardScene.QueueFree(); // Ensure memory is cleaned up
		}
		CardScenes.Clear();
	}

	public void ShowCardEmphasis(int cardId)
	{        
		// Only access CardPreview if it's been initialized (in LoadUI)
		if (CardPreview != null)
		{
			CardPreview.ShowCard(cardId);
		}
	}
	public void HideCardEmphasis()
	{
		// Only access CardPreview if it's been initialized (in LoadUI)
		if (CardPreview != null)
		{
			CardPreview.Visible = false;
			CardPreview.MouseFilter = MouseFilterEnum.Ignore;
		}
	}

	private void UnsubscribeFromEvents()
	{
		// Unsubscribe from EventBus events
		if (EventBus.Instance != null && OnNextStepStarted != null)
		{
			EventBus.Instance.NextStepStarted -= OnNextStepStarted;
		}

		// Unsubscribe from button events
		if (IsInstanceValid(TestButton) && onTestButtonPressed != null)
		{
			TestButton.Pressed -= onTestButtonPressed;
		}
	}
}
