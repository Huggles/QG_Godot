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

	/// <summary>
	/// The faction the display is following on its own — see <see cref="OnTurnStepStarted"/>. Distinct
	/// from <see cref="showingFaction"/>, which every prompt and every browse overwrites: this one only
	/// ever moves when the upcoming faction does, so a turn's worth of steps is a no-op.
	/// </summary>
	private Faction followedFaction = Faction.NONE;

	[Signal] public delegate void CardSelectedEventHandler(int cardId);

	// Event handlers for cleanup
	private Action onTestButtonPressed;

	/// <summary>
	/// Whether <see cref="LoadUI"/> connected. Only the authority peer does, so tearing down
	/// unconditionally would disconnect signals that were never connected.
	/// </summary>
	private bool _subscribed;

	


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
		// Callers guard on `Current == null` (InputManager.RefreshCardPrompt, BottomLeftMenu) — a
		// pointer left on a freed node from the previous game passes that guard and then throws.
		if (Current == this) Current = null;

		UnsubscribeFromEvents();
	}

	private void LoadUI()
	{
		Hide();

		// Unsubscribe first to prevent duplicate connections
		UnsubscribeFromEvents();

		EventBus.Instance.GameSessionStarted += OnGameSessionStarted;
		EventBus.Instance.CardsDrawn += OnCardsDrawn;
		EventBus.Instance.CardsDiscarded += OnCardsDiscarded;
		EventBus.Instance.NextStepStarted += OnTurnStepStarted;
		_subscribed = true;

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
		Faction upcoming = UpcomingLocalFaction();
		DebugUtilities.PrintPeerFinest($"Game session started, showing hand display for upcoming faction: {upcoming}");
		if (upcoming == Faction.NONE) return;

		followedFaction = upcoming;
		Show(upcoming);
	}

	/// <summary>
	/// Follow the hand of the next faction this peer will actually be asked to act for, so a player who
	/// has just ended a turn is looking at what they will play next rather than at the hand they have
	/// finished with. The cards are not selectable yet — the point is to be able to plan.
	///
	/// Driven by NextStepStarted rather than NewTurnStarted: the latter is emitted from
	/// GameFlow.StartNewTurn, which only the host runs, while TurnStepCounter is a replicated property
	/// whose setter emits this on every peer — and on the resume after a save restore, where the turn
	/// has already moved before any new event lands.
	///
	/// The step itself is ignored. Only a change of upcoming faction acts, which is what keeps this out
	/// of the way of the turn it is already in: every step of a running turn resolves to the same
	/// faction, so the display stays exactly where that turn's prompts left it. It follows that a peer
	/// controlling one faction never sees this move at all — its next action is always that same hand.
	/// </summary>
	private void OnTurnStepStarted(int turnStep)
	{
		Faction upcoming = UpcomingLocalFaction();
		if (upcoming == Faction.NONE || upcoming == followedFaction) return;

		// An open card prompt owns this display, and so does a hand pulled up from the bottom-left menu
		// (which is claimed over a live request rather than instead of one, hence IsHeldBy rather than
		// FactionFocus.Current). Deliberately not recorded as followed when skipped: the next step
		// retries, so the display catches up as soon as it is free again.
		if (InputManager.CurrentCardPrompt != null || FactionFocus.IsHeldBy(FactionFocusSource.Browsing))
		{
			return;
		}

		// Guarded through FactionState because Show -> DeckState.ForFaction dereferences it, and it is
		// null until the game state has arrived on this peer.
		if (FactionState.ForEnum(upcoming)?.DeckState == null) return;

		followedFaction = upcoming;
		Show(upcoming);
	}

	/// <summary>
	/// The first faction at or after <see cref="GameFlow.CurrentFaction"/>, in turn order, that this
	/// peer controls — the next hand it will be prompted to play from. Resolves to the current faction
	/// for as long as its own turn is running, which is why ending that turn is what moves it on.
	/// </summary>
	private static Faction UpcomingLocalFaction()
	{
		List<Faction> controlledFactions = PlayerScene.Current?.ControlledFactions;
		if (controlledFactions == null || controlledFactions.Count == 0 || GameFlow.Instance == null)
		{
			return Faction.NONE;
		}

		List<Faction> turnOrder = StaticGameData.PlayableFactions;
		// Max(_, 0): CurrentFaction is GERMANY before the first turn has started, which is turnOrder[0]
		// anyway, but an unplayable faction must not send the scan below off the front of the list.
		int start = Mathf.Max(turnOrder.IndexOf(GameFlow.Instance.CurrentFaction), 0);

		for (int offset = 0; offset < turnOrder.Count; offset++)
		{
			Faction candidate = turnOrder[(start + offset) % turnOrder.Count];
			if (controlledFactions.Contains(candidate)) return candidate;
		}
		return Faction.NONE;
	}


	/// <summary>
	/// Not connected. It used to be subscribed in <see cref="LoadUI"/> and then immediately dropped
	/// again by the UnsubscribeFromEvents call on the next line, so it has never actually run; the
	/// pair is gone but the handler is kept because the hand is meant to follow the turn step.
	/// Reconnect it deliberately, not as a side effect of a cleanup fix — the default branch hides the
	/// hand on every step that is not START or PLAY_CARD, which would change how prompts behave.
	/// </summary>
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
	/// <param name="isReactionWindow">
	/// A block or after-reaction prompt. Every selectable card in one gets the foil sweep — see
	/// LayoutFan. Nothing to do with how the fan is laid out; it is purely which cue the prompt earns.
	/// </param>
	public void Show(List<int> cardIds, Faction faction, List<int> selectableCardIds = null,
		bool separateNonHandCards = false, bool isReactionWindow = false)
	{
		showingFaction = faction;
		ResetVisibility();
		InitCards(cardIds, selectableCardIds, separateNonHandCards, isReactionWindow);
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
		bool separateNonHandCards = false, bool isReactionWindow = false)
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

		LayoutFan(handIds, selectableCardIds, containerCentreX, fanTopY, HandCardStepSize, HandCardScale, 0,
			isReactionWindow);
		LayoutFan(sideIds, selectableCardIds, miniCentreX, fanTopY, miniStepSize, MiniCardScale, handIds.Count,
			isReactionWindow);
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
		float stepSize, float scale, int zIndexOffset, bool isReactionWindow = false)
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

			// The foil sweep marks a card worth acting on, in the two prompts where one is easy to miss:
			//
			//   - Any reaction window (block or after): every card that can actually take the reaction.
			//     The window is a fleeting chance offered mid-someone-else's-action, and the always-ask
			//     rule means most of these prompts are pure cover with nothing to play — so the rare one
			//     that does offer something should not look like the rest.
			//   - The play prompt: only a play-step activation that does NOT spend the play
			//     (CardLogic.IsFreePlayStepActivation — the four cards that read "at the beginning of
			//     your turn"). Passing one over is a pure loss, and they are easy to miss now that they
			//     sit in the side fan rather than getting a prompt of their own at TurnStep.START.
			//     Deliberately not every activatable card here: the hand is the prompt's whole subject,
			//     so gilding all of it would say nothing.
			//
			// Gated on selectable either way: emphasising a card the player then cannot click reads as a
			// bug, and the greyed-out copy is already explained by the availability scrim. A cautioned
			// card still gleams — it IS usable, and Caution is a separate statement about its value.
			//
			// And gated on IsPlayed: the cue is only ever about a card ON THE TABLE. A Status/Response
			// card still in hand is a normal hand play like any other, and both tests above would
			// otherwise admit it — IsFreePlayStepActivation is a constant on the CardLogic that says
			// nothing about where the card is (Defense of the Motherland gleamed in hand for exactly
			// this reason), and a hand card can be selectable in the play prompt.
			//
			// Set on every card, not just the emphasised ones — a CardScene is fresh here, but
			// SetEmphasized is what puts the overlay into a known state.
			cardSceneInstance.SetEmphasized(selectable && cardState.IsPlayed
				&& (isReactionWindow || cardState.CardLogic?.IsFreePlayStepActivation == true));
		}
	}

	private void OnCardSelected(int cardId)
	{
		DebugUtilities.PrintPeer($"Card selected with ID: {cardId}");
		EmitSignal(SignalName.CardSelected, cardId);
	}

	private void DeleteCurrentCards()
	{
		// The nodes about to be freed are the only things that could ever fire the MouseExited that
		// clears a hover preview, so a glow raised from one of them would otherwise be permanent.
		CardTargetPreviewDisplay.ClearAll();

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

	/// <summary>
	/// Drops every EventBus connection this node holds. EventBus is a process-wide static, so a
	/// connection left behind outlives the game scene — and because a Godot C# signal runs all of its
	/// handlers through one multicast delegate, the first stale handler to throw
	/// ObjectDisposedException aborts the emission for everyone behind it. Quitting and loading a
	/// second game used to lose the faction strip that way: the previous game's hand display threw on
	/// GameSessionStarted before the new FactionsContainer's handler was ever reached.
	/// </summary>
	private void UnsubscribeFromEvents()
	{
		if (!_subscribed || EventBus.Instance == null) return;
		_subscribed = false;

		EventBus.Instance.GameSessionStarted -= OnGameSessionStarted;
		EventBus.Instance.CardsDrawn -= OnCardsDrawn;
		EventBus.Instance.CardsDiscarded -= OnCardsDiscarded;
		EventBus.Instance.NextStepStarted -= OnTurnStepStarted;
	}
}
