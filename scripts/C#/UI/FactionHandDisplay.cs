using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class FactionHandDisplay : Control
{

	public static FactionHandDisplay Current;

	// Hand fan geometry, unchanged from when it was inline in InitCards.
	private const float HandCardStepSize = 60f;
	private const float RotationStepSize = 10f;
	private const float HandCardScale = 0.3f;

	// The side fan: the same fan, scaled down, for the cards a prompt offers that are not in hand.
	// The step scales with the cards so the overlap looks the same at both sizes.
	private const float MiniCardScale = 0.3f;
	private const float FanGap = 60f;
	private const float FanEdgeMargin = 20f;

	private Control CardsContainer => GetNode<Panel>("%CardsContainerPanel");
	private Panel CardPreviewContainer => GetNode<Panel>("%CardPreviewContainer");
	private CardScene CardPreview => GetNode<CardScene>("%CardPreview");

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
			
			_ = ModalStack.Current.Show(
				ModalConfig.Display("Select a faction", PresentationItemImageButton.ForFactions([Faction.GERMANY,Faction.JAPAN]))
					.WithDedupeKey("debug:select-faction"));
		};
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
	/// <param name="separateNonHandCards">
	/// Draw the cards that are NOT in the faction's hand as a second, scaled-down fan to the right of
	/// the hand instead of mixing them in. Only the hand-play prompt wants this: its offer set spans
	/// two zones (see InputRequest.HandCardPlayRequestHandler.PopulateTargets), so a Status card
	/// already on the table was drawn identically to a hand card, at a position decided only by its id.
	///
	/// False everywhere else, and it must stay false for the table-cards-only prompts — an
	/// ActivateCardRequestHandler or reaction window offers nothing from hand, so splitting one would
	/// move every card to the side fan and leave the hand position empty.
	/// </param>
	public void Show(List<int> cardIds, Faction faction, List<int> selectableCardIds = null,
		bool separateNonHandCards = false)
	{
		showingFaction = faction;
		ResetVisibility();
		InitCards(cardIds, selectableCardIds, separateNonHandCards);
	}

	private void ResetVisibility()
	{
		Visible = true;
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

	private void InitCards(List<int> cardIds, List<int> selectableCardIds = null,
		bool separateNonHandCards = false)
	{
		// Only initialize cards if LoadUI has been called
		DebugUtilities.PrintPeerFinest($"Initializing cards {string.Join(", ", cardIds)}");
		if (CardsContainer == null)
		{
			return;
		}

		DeleteCurrentCards();

		// Copies, not the caller's list: the sort below used to reorder DeckState.HandCardIds itself,
		// since Show(Faction) hands over the backing list.
		List<int> handIds = new List<int>(cardIds);
		List<int> sideIds = new List<int>();

		if (separateNonHandCards)
		{
			// Presentational only. Which zone a card sits in is not a legality question — selectability
			// still comes from the host's list further down — and the prompted faction's own hand is
			// replicated to this peer. Guarded through FactionState because DeckState.ForFaction
			// dereferences it, and it is null until the game state has arrived; with no DeckState
			// nothing is split and the layout is exactly what it was.
			List<int> factionHandCardIds = FactionState.ForEnum(showingFaction)?.DeckState?.HandCardIds;
			if (factionHandCardIds != null)
			{
				handIds = cardIds.Where(factionHandCardIds.Contains).ToList();
				sideIds = cardIds.Where(cardId => !factionHandCardIds.Contains(cardId)).ToList();
			}
		}

		handIds.Sort();
		sideIds.Sort();

		float containerWidth = ContainerWidth();
		float containerCentreX = containerWidth * 0.5f;
		float miniStepSize = HandCardStepSize * (MiniCardScale / HandCardScale);
		float miniHalfWidth = FanHalfWidth(sideIds.Count, miniStepSize, MiniCardScale);

		// The hand keeps the centre it has always had, so cards never shift under the cursor when a
		// table card becomes activatable or stops being so.
		float miniCentreX = handIds.Count == 0
			? containerCentreX
			: containerCentreX + FanHalfWidth(handIds.Count, HandCardStepSize, HandCardScale)
				+ FanGap + miniHalfWidth;
		// A hand wide enough to push the side fan off screen is worth an overlap, not a fan nobody can see.
		miniCentreX = Mathf.Min(miniCentreX, containerWidth - miniHalfWidth - FanEdgeMargin);

		// Both fans hang from one top edge. The hand's own bottom is deliberately clipped by the screen
		// edge, so bottom-aligning the smaller fan instead would cut nearly half of it away.
		// -Size.Y/3 + halfHeight * (1 - HandCardScale) is literally the hand's top edge today.
		float fanTopY = -CardScene.DEFAULT_SIZE.Y / 3f
			+ CardScene.DEFAULT_SIZE.Y * 0.5f * (1f - HandCardScale);

		LayoutFan(handIds, selectableCardIds, containerCentreX, fanTopY, HandCardStepSize, HandCardScale, 0);
		LayoutFan(sideIds, selectableCardIds, miniCentreX, fanTopY, miniStepSize, MiniCardScale, handIds.Count);
	}

	/// <summary>
	/// CardsContainer's width, or the viewport's before the first layout pass has given it one. The
	/// hand has always centred on this, so a zero would already have drawn wrong — but the side fan's
	/// right-edge clamp would additionally turn a zero into a negative X.
	/// </summary>
	private float ContainerWidth()
	{
		float width = CardsContainer.Size.X;
		return width > 1f ? width : GetViewportRect().Size.X;
	}

	/// <summary>
	/// Half the drawn width of a fan of <paramref name="count"/> cards, rotation included. Scale and
	/// rotation are both applied about PivotOffset, which for CardScene is the card's centre, so each
	/// card's drawn half-width is the rotated rect's AABB: halfW·|cos θ| + halfH·|sin θ|. Dropping the
	/// rotation term underestimates by ~70px at seven cards, which is enough to drop the side fan on
	/// top of the last hand card. The fan is symmetric, so one half serves both edges.
	/// </summary>
	private static float FanHalfWidth(int count, float stepSize, float scale)
	{
		if (count == 0) return 0f;

		float halfWidth = CardScene.DEFAULT_SIZE.X * 0.5f * scale;
		float halfHeight = CardScene.DEFAULT_SIZE.Y * 0.5f * scale;
		float totalRotationSize = (count - 1) * RotationStepSize;
		float fanHalfWidth = 0f;

		// Max over every card rather than assuming the outermost one wins: that holds for the
		// rotations in use but stops holding once a fan is wide enough to pass ~55°.
		for (int index = 0; index < count; index++)
		{
			float pivotX = stepSize * index - (count - 1) * stepSize * 0.5f;
			float theta = Mathf.DegToRad(index * RotationStepSize - totalRotationSize * 0.5f);
			fanHalfWidth = Mathf.Max(fanHalfWidth,
				pivotX + halfWidth * Mathf.Abs(Mathf.Cos(theta)) + halfHeight * Mathf.Abs(Mathf.Sin(theta)));
		}
		return fanHalfWidth;
	}

	/// <summary>
	/// Fan <paramref name="cardIds"/> out around <paramref name="centreX"/>, hanging from
	/// <paramref name="topY"/> — both in CardsContainer space. Cards are always instantiated at
	/// CardScene.DEFAULT_SIZE and shrunk with Scale, because CardScene.RecalculateSizes derives its
	/// font sizes from Size.Y and the .tscn is authored against the default.
	/// </summary>
	/// <param name="zIndexOffset">
	/// Keeps the side fan above the hand wherever the clamp has had to overlap the two.
	/// </param>
	private void LayoutFan(List<int> cardIds, List<int> selectableCardIds, float centreX, float topY,
		float stepSize, float scale, int zIndexOffset)
	{
		float totalRotationSize = (cardIds.Count - 1) * RotationStepSize;
		float totalSizeX = (cardIds.Count - 1) * stepSize;

		foreach (var (cardId, index) in cardIds.Select((cardId, index) => (cardId, index)))
		{
			CardState cardState = CardState.ForId(cardId);
			var cardSceneInstance = CardScene.CardScenePackedPath.Instantiate<CardScene>();
			cardSceneInstance.Size = CardScene.DEFAULT_SIZE;
			cardSceneInstance.Scale = new Vector2(scale, scale);
			CardsContainer.AddChild(cardSceneInstance);

			Vector2 basePosition = new Vector2(centreX, 0);
			basePosition.X += stepSize * index;
			basePosition.X -= (totalSizeX / 2) + cardSceneInstance.PivotOffset.X;
			basePosition.Y = topY - cardSceneInstance.PivotOffset.Y * (1f - scale);


			cardSceneInstance.Position = basePosition;
			cardSceneInstance.ZIndex = zIndexOffset + index;
			cardSceneInstance.RotationDegrees = index * RotationStepSize - (totalRotationSize / 2f);

			cardSceneInstance.Selected += OnCardSelected;
			CardScenes.Add(cardSceneInstance);
			cardSceneInstance.ShowCard(cardId);

			// The caller's list wins when it gave one. Tag.IsActivatable is the right default for the
			// plain hand display, but it is the wrong answer inside a prompt: block-eligible cards
			// carry Tag.IsBlockReaction, which is server-internal and never reaches a client, so a
			// card the host is offering would render greyed out and refuse the click.
			bool selectable = selectableCardIds?.Contains(cardId)
							  ?? cardState.HasTag(Tag.IsActivatable, cardState.Faction);

			// Read from the tag rather than from selectableCardIds: a cautioned card IS selectable and so
			// IS in the host's offered set. The caution is an extra fact about it, not a third value of
			// the same question.
			bool needsAttention = selectable && cardState.HasTag(Tag.NeedsAttention, cardState.Faction);

			cardSceneInstance.SetClickable(selectable);
			cardSceneInstance.SetAvailability(
				!selectable    ? CardScene.CardAvailability.Unavailable :
				needsAttention ? CardScene.CardAvailability.Caution
							   : CardScene.CardAvailability.Available);
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
	}
}
