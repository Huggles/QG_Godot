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
	private const float MiniCardScale = 0.2f;
	private const float FanGap = 60f;
	private const float FanEdgeMargin = 20f;
	// How far below the hand's centre line the side fan sits.
	private const float MiniFanDropY = 40f;

	private Control CardsContainer => GetNode<Panel>("%CardsContainerPanel");
	private Panel CardPreviewContainer => GetNode<Panel>("%CardPreviewContainer");
	private CardScene CardPreview => GetNode<CardScene>("%CardPreview");

	private List<CardScene> CardScenes = new List<CardScene>();
	private Faction showingFaction;

	// Swap animation: a card leaving the fan fades and sinks, a card joining it rises from below and
	// fades in (staggered), and a card in both the old and new fan slides from where it was.
	private const float LeaveSeconds = 0.18f;
	private const float LeaveDrop = 40f;
	private const float EnterSeconds = 0.32f;
	private const float EnterRise = 90f;
	private const float EnterStagger = 0.035f;
	private const float SlideSeconds = 0.2f;

	private readonly record struct CardPose(Vector2 Position, float Rotation, Vector2 Scale, float Alpha);

	/// <summary>Which fan entry each live CardScene stands for, so the next draw can tell kept cards from new ones.</summary>
	private readonly Dictionary<CardScene, string> cardKeys = new();
	private readonly Dictionary<CardScene, Tween> cardTweens = new();
	/// <summary>Where each kept card was when the fan was torn down; consumed by LayoutFan.</summary>
	private readonly Dictionary<string, CardPose> previousPoses = new();
	private int enterOrder;
	private bool anyLeaving;
	// Bumped by every Show, so a fade-out started by Hide cannot hide a display that was re-shown meanwhile.
	private int visibilityGeneration;

	/// <summary>
	/// The faction the display is following on its own — see <see cref="OnTurnStepStarted"/>. Distinct
	/// from <see cref="showingFaction"/>, which every prompt and every browse overwrites: this one only
	/// ever moves when the upcoming faction does, so a turn's worth of steps is a no-op.
	/// </summary>
	private Faction followedFaction = Faction.NONE;

	[Signal] public delegate void CardSelectedEventHandler(int cardId);
	[Signal] public delegate void PickConfirmedEventHandler();

	// A pick prompt (a discard): clicks toggle cards, which rise out of the fan along their own tilt,
	// and a confirm button floats above the fan.
	private const float PickRise = 45f;
	private const float PickSeconds = 0.14f;
	private const float PickButtonGap = 14f;
	private static readonly Vector2 PickButtonSize = new(200f, 32f);
	private CardPick activePick;
	private MenuPanelButton pickButton;
	private Label pickHint;
	// The wave that runs once over the fan when a pick opens: each card swells and settles in turn.
	private const float WaveStart = 0.45f;
	private const float WaveStagger = 0.07f;
	private const float WaveSwell = 1.12f;
	/// <summary>Each card's resting pose in the fan, which a picked card rises from.</summary>
	private readonly Dictionary<CardScene, Vector2> restPositions = new();
	private float fanTopY;
	private float fanCentreX;

	// Event handlers for cleanup
	private Action onTestButtonPressed;

	/// <summary>
	/// Whether <see cref="LoadUI"/> connected. Only the authority peer does, so tearing down
	/// unconditionally would disconnect signals that were never connected.
	/// </summary>
	private bool _subscribed;

	


	public override void _Ready()
	{        
		if(SessionIdentity.IsLocalAuthority(this))
		{
			Current = this;
			LoadUI();          
		}        
	}  


	public override void _ExitTree()
	{
		// Callers guard on `Current == null` (InputManager.RefreshCardPrompt, MiniMenu) — a
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
		CreatePickButton();

		onTestButtonPressed = () =>
		{
			if (!IsInstanceValid(this) || !IsInsideTree()) return;
			
			_ = ModalStack.Current.Show(
				ModalConfig.Display("Select a faction", PresentationItemImageButton.ForFactions([Faction.GERMANY,Faction.JAPAN]))
					.WithDedupeKey("debug:select-faction"));
		};
	}

	private void OnCardsDrawn(int faction, int numberOfCards) => RefreshFor((Faction)faction);

	private void OnCardsDiscarded(int faction, int numberOfCards) => RefreshFor((Faction)faction);

	/// <summary>
	/// Re-draw the hand after cards moved, if this display is the one showing that faction.
	///
	/// An open card prompt owns this display, so it is redrawn as the PROMPT rather than as a plain
	/// hand — Show(Faction) would replace an offer the player is in the middle of answering with an
	/// unclickable copy of one faction's hand, and the request awaiting CardSelected would never be
	/// released. The same claim OnTurnStepStarted respects.
	/// </summary>
	private void RefreshFor(Faction faction)
	{
		// A hand pulled up from the mini menu is claimed OVER a live prompt rather than instead
		// of one, so browsing is checked first and keeps the behaviour it always had: refresh what is
		// actually on the display.
		bool browsing = HandBrowsing.IsActive;

		if (!browsing && InputManager.CurrentCardPrompt != null)
		{
			if (InputManager.CurrentCardPrompt.Answering.Contains(faction))
				InputManager.ShowCurrentCardPrompt();
			return;
		}

		if (showingFaction == faction) Show(showingFaction);
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

		// An open card prompt owns this display, and so does a hand pulled up from the mini menu
		// (claimed over a live request rather than instead of one, so browsing being set says nothing
		// about whether a prompt is open underneath it). Deliberately not recorded as followed when
		// skipped: the next step retries, so the display catches up as soon as it is free again.
		if (InputManager.CurrentCardPrompt != null || HandBrowsing.IsActive)
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
		activePick = null;
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
	/// <param name="answeringFactions">
	/// The factions a merged reaction prompt is asking, in the order they should appear. The fan is
	/// sorted by owner within this order so each faction's cards sit together instead of interleaving by
	/// card id — a player holding Germany, Japan and Italy answers all three in one prompt, and ten
	/// cards shuffled by id is not something anyone can read.
	///
	/// Deliberately still ONE fan. LayoutFan positions by index off a single centre, and a fan per
	/// faction would mean generalising the fan geometry and the right-edge clamp for N fans that would
	/// overflow the screen anyway. Null (every prompt but a merged window) keeps the plain id sort.
	/// </param>
	/// <param name="optionCards">Options drawn as cards after the card ids, in the side fan when the prompt splits one off.</param>
	/// <param name="pick">A multi-card pick: clicks toggle cards instead of answering, and the confirm button shows.</param>
	public void Show(List<int> cardIds, Faction faction, List<int> selectableCardIds = null,
		bool separateNonHandCards = false, bool isReactionWindow = false,
		IReadOnlyList<Faction> answeringFactions = null, List<InputRequest.OptionCard> optionCards = null,
		CardPick pick = null)
	{
		showingFaction = faction;
		bool pickOpened = pick != null && pick != activePick;
		activePick = pick;
		pick?.KeepOnly(cardIds);
		ResetVisibility();
		InitCards(cardIds, selectableCardIds, separateNonHandCards, isReactionWindow, answeringFactions, optionCards);
		UpdatePickButton();
		if (pickOpened) WaveCards();
	}

	/// <summary>
	/// Swells each card in turn once the fan has settled, so a new pick reads as "click these". Scale
	/// rather than position, so it cannot fight the pick tweens that move a card up or down.
	/// </summary>
	private void WaveCards()
	{
		foreach (var (card, index) in CardScenes.Select((card, index) => (card, index)))
		{
			if (!card.IsInsideTree()) continue;
			Vector2 rest = card.Scale;
			Tween wave = card.CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
			wave.TweenInterval(WaveStart + index * WaveStagger);
			wave.TweenProperty(card, "scale", rest * WaveSwell, 0.14f);
			wave.TweenProperty(card, "scale", rest, 0.22f).SetEase(Tween.EaseType.InOut);
		}
	}

	private void ResetVisibility()
	{
		visibilityGeneration++;
		Visible = true;
		HideCardEmphasis();
	}

	/// <summary>
	/// Fades the cards out, then hides the display — unless a Show arrives first. The cards stop taking
	/// clicks at once; only their fade-out outlives this call.
	/// </summary>
	public new void Hide()
	{
		// The trigger context is no longer a child of this node and is deliberately NOT cleared here:
		// a mutator's Bulletin has to outlive the hand display, which every step transition hides.
		// InputManager.HandleItemSelected clears it for card prompts, InputRequest.Execute for the rest.
		HideCardEmphasis();
		activePick = null;
		UpdatePickButton();

		// Only access CardsContainer if it's been initialized (in LoadUI)
		if (CardsContainer != null)
		{
			CardsContainer.MouseFilter = MouseFilterEnum.Ignore;
			DeleteCurrentCards(new HashSet<string>());
		}

		if (!anyLeaving || !IsInsideTree())
		{
			Visible = false;
			return;
		}
		int generation = visibilityGeneration;
		GetTree().CreateTimer(LeaveSeconds).Timeout += () =>
		{
			if (IsInstanceValid(this) && generation == visibilityGeneration) Visible = false;
		};
	}

	private void InitCards(List<int> cardIds, List<int> selectableCardIds = null,
		bool separateNonHandCards = false, bool isReactionWindow = false,
		IReadOnlyList<Faction> answeringFactions = null, List<InputRequest.OptionCard> optionCards = null)
	{
		// Only initialize cards if LoadUI has been called
		DebugUtilities.PrintPeerFinest($"Initializing cards {string.Join(", ", cardIds)}");
		if (CardsContainer == null)
		{
			return;
		}

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

		SortForDisplay(handIds, answeringFactions);
		SortForDisplay(sideIds, answeringFactions);

		List<FanEntry> handFan = handIds.Select(FanEntry.ForCard).ToList();
		List<FanEntry> sideFan = sideIds.Select(FanEntry.ForCard).ToList();
		// An option is never a hand card, so a split prompt draws it in the side fan.
		(separateNonHandCards ? sideFan : handFan).AddRange((optionCards ?? new()).Select(FanEntry.ForOption));

		DeleteCurrentCards(handFan.Concat(sideFan).Select(entry => entry.Key).ToHashSet());
		enterOrder = 0;

		float containerWidth = ContainerWidth();
		float containerCentreX = containerWidth * 0.5f;
		float miniStepSize = HandCardStepSize * (MiniCardScale / HandCardScale);
		float miniHalfWidth = FanHalfWidth(sideFan.Count, miniStepSize, MiniCardScale);

		// The hand keeps the centre it has always had, so cards never shift under the cursor when a
		// table card becomes activatable or stops being so.
		float miniCentreX = handFan.Count == 0
			? containerCentreX
			: containerCentreX + FanHalfWidth(handFan.Count, HandCardStepSize, HandCardScale)
				+ FanGap + miniHalfWidth;
		// A hand wide enough to push the side fan off screen is worth an overlap, not a fan nobody can see.
		miniCentreX = Mathf.Min(miniCentreX, containerWidth - miniHalfWidth - FanEdgeMargin);

		// The side fan hangs from the hand's vertical centre (plus MiniFanDropY), not its top edge.
		// -Size.Y/3 + halfHeight is literally the hand's centre today.
		float fanCentreY = -CardScene.DEFAULT_SIZE.Y / 3f + CardScene.DEFAULT_SIZE.Y * 0.5f;
		fanCentreX = containerCentreX;
		fanTopY = fanCentreY - CardScene.DEFAULT_SIZE.Y * 0.5f * HandCardScale;

		LayoutFan(handFan, selectableCardIds, containerCentreX, fanCentreY, HandCardStepSize, HandCardScale, 0,
			isReactionWindow);
		LayoutFan(sideFan, selectableCardIds, miniCentreX, fanCentreY + MiniFanDropY, miniStepSize, MiniCardScale, handFan.Count,
			isReactionWindow);
	}

	/// <summary>One card of a fan: a real card by id, or an option drawn as one.</summary>
	private readonly record struct FanEntry(int CardId, InputRequest.OptionCard Option)
	{
		public static FanEntry ForCard(int cardId) => new(cardId, null);
		public static FanEntry ForOption(InputRequest.OptionCard option) => new(-1, option);
		/// <summary>Identity across redraws: the same card or option keeps its key from one prompt to the next.</summary>
		public string Key => Option != null ? $"o{Option.OptionId}" : $"c{CardId}";
	}

	/// <summary>
	/// Order a fan in place: by owning faction first when a merged prompt named several, then by card id
	/// as it has always been. An owner outside <paramref name="answeringFactions"/> sorts last rather
	/// than throwing — the display set can legitimately include a card the offer did not
	/// (see CardPlayRound.ReactionWindowDisplayCardIds).
	/// </summary>
	private static void SortForDisplay(List<int> cardIds, IReadOnlyList<Faction> answeringFactions)
	{
		if (answeringFactions == null || answeringFactions.Count < 2)
		{
			cardIds.Sort();
			return;
		}

		cardIds.Sort((left, right) =>
		{
			int byFaction = OwnerRank(left).CompareTo(OwnerRank(right));
			return byFaction != 0 ? byFaction : left.CompareTo(right);
		});

		int OwnerRank(int cardId)
		{
			CardState card = CardState.ForId(cardId);
			if (card == null) return answeringFactions.Count;
			for (int i = 0; i < answeringFactions.Count; i++)
				if (answeringFactions[i] == card.Faction) return i;
			return answeringFactions.Count;
		}
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
	/// Fan <paramref name="cardIds"/> out around <paramref name="centreX"/>, centred on
	/// <paramref name="centreY"/> — both in CardsContainer space. Cards are always instantiated at
	/// CardScene.DEFAULT_SIZE and shrunk with Scale, because CardScene.RecalculateSizes derives its
	/// font sizes from Size.Y and the .tscn is authored against the default.
	/// </summary>
	/// <param name="zIndexOffset">
	/// Keeps the side fan above the hand wherever the clamp has had to overlap the two.
	/// </param>
	private void LayoutFan(List<FanEntry> entries, List<int> selectableCardIds, float centreX, float centreY,
		float stepSize, float scale, int zIndexOffset, bool isReactionWindow = false)
	{
		float totalRotationSize = (entries.Count - 1) * RotationStepSize;
		float totalSizeX = (entries.Count - 1) * stepSize;

		foreach (var (entry, index) in entries.Select((entry, index) => (entry, index)))
		{
			int cardId = entry.CardId;
			var cardSceneInstance = CardScene.CardScenePackedPath.Instantiate<CardScene>();
			cardSceneInstance.Size = CardScene.DEFAULT_SIZE;
			cardSceneInstance.Scale = new Vector2(scale, scale);
			CardsContainer.AddChild(cardSceneInstance);

			Vector2 basePosition = new Vector2(centreX, 0);
			basePosition.X += stepSize * index;
			basePosition.X -= (totalSizeX / 2) + cardSceneInstance.PivotOffset.X;
			basePosition.Y = centreY - cardSceneInstance.PivotOffset.Y;


			cardSceneInstance.Position = basePosition;
			cardSceneInstance.ZIndex = zIndexOffset + index;
			cardSceneInstance.RotationDegrees = index * RotationStepSize - (totalRotationSize / 2f);

			restPositions[cardSceneInstance] = basePosition;
			if (entry.Option == null && activePick?.Selected.Contains(cardId) == true)
				cardSceneInstance.Position = PickedPosition(cardSceneInstance);

			cardSceneInstance.Selected += OnCardSelected;
			CardScenes.Add(cardSceneInstance);
			cardKeys[cardSceneInstance] = entry.Key;
			AnimateIn(cardSceneInstance, entry.Key);

			// An option has no CardState, so no tag can grey it out: the option itself says whether it is offered.
			if (entry.Option != null)
			{
				cardSceneInstance.ShowOption(entry.Option, CardFace.ForOption(entry.Option, showingFaction));
				cardSceneInstance.SetClickable(entry.Option.Available);
				cardSceneInstance.SetAvailability(entry.Option.Available
					? CardScene.CardAvailability.Available : CardScene.CardAvailability.Unavailable);
				cardSceneInstance.SetEmphasized(false);
				continue;
			}

			CardState cardState = CardState.ForId(cardId);
			cardSceneInstance.ShowCard(cardId);
			cardSceneInstance.SetRecentlyDrawn(cardState.IsRecentlyDrawn
				&& BoardState.Live.ForFaction(cardState.Faction).Hand.Contains(cardId));

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
		if (activePick != null)
		{
			TogglePick(cardId);
			return;
		}
		EmitSignal(SignalName.CardSelected, cardId);
	}

	/// <summary>Picks or unpicks a card in the open pick prompt and moves every card that changed.</summary>
	private void TogglePick(int cardId)
	{
		foreach (int changedId in activePick.Toggle(cardId))
		{
			CardScene card = CardScenes.FirstOrDefault(scene => scene.CardId == changedId);
			if (card == null || !restPositions.ContainsKey(card)) continue;

			if (cardTweens.Remove(card, out Tween running)) running.Kill();
			Vector2 target = activePick.Selected.Contains(changedId) ? PickedPosition(card) : restPositions[card];
			Tween tween = card.CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			tween.TweenProperty(card, "position", target, PickSeconds);
			cardTweens[card] = tween;
			tween.Finished += () => cardTweens.Remove(card);
		}
		UpdatePickButton();
	}

	/// <summary>A picked card's pose: its resting place, pushed out along the card's own up direction.</summary>
	private Vector2 PickedPosition(CardScene card)
		=> restPositions[card] + new Vector2(Mathf.Sin(card.Rotation), -Mathf.Cos(card.Rotation)) * PickRise;

	/// <summary>The pick prompt's confirm button, built in code so both UI scenes that host the hand get it.</summary>
	private void CreatePickButton()
	{
		if (pickButton != null || CardsContainer == null) return;

		pickButton = GD.Load<PackedScene>("res://scenes/menu/MenuPanelButton.tscn").Instantiate<MenuPanelButton>();
		pickButton.Name = "PickConfirmButton";
		pickButton.CustomMinimumSize = PickButtonSize;
		pickButton.Size = PickButtonSize;
		pickButton.ZIndex = 50;
		pickButton.HeldContentOffset = 2;
		pickButton.HoveredContentTint = new Color(1.03f, 0.91f, 0.67f);
		pickButton.AddThemeStyleboxOverride("normal", PickButtonStyle(new Color(0.047f, 0.051f, 0.039f, 0.88f), new Color(0.851f, 0.678f, 0.361f, 0.6f)));
		pickButton.AddThemeStyleboxOverride("hover", PickButtonStyle(new Color(0.2f, 0.17f, 0.1f, 0.92f), new Color(0.851f, 0.678f, 0.361f, 0.95f)));
		pickButton.AddThemeStyleboxOverride("pressed", PickButtonStyle(new Color(0.2f, 0.17f, 0.1f, 0.92f), new Color(0.851f, 0.678f, 0.361f, 0.95f)));
		pickButton.AddThemeStyleboxOverride("disabled", PickButtonStyle(new Color(0.047f, 0.051f, 0.039f, 0.6f), new Color(1f, 1f, 1f, 0.1f)));
		pickButton.Visible = false;
		// Before entering the tree: MenuPanelButton raises its minimum size to its frame art every
		// frame, which would make this a 150x80 block over the raised cards.
		if (pickButton.GetNodeOrNull("NinePatchRect") is Node frame)
		{
			pickButton.RemoveChild(frame);
			frame.QueueFree();
		}
		if (pickButton.GetNodeOrNull<MarginContainer>("ContentContainer") is MarginContainer content)
			foreach (string side in new[] { "left", "top", "right", "bottom" })
				content.AddThemeConstantOverride($"margin_{side}", 4);
		CardsContainer.AddChild(pickButton);

		if (pickButton.GetNodeOrNull<Label>("ContentContainer/Label") is Label label)
			label.LabelSettings = new LabelSettings
			{
				Font = GD.Load<Font>("res://scenes/menu/theme/MenuFontBody.tres"),
				FontSize = 16,
				FontColor = new Color(0.929f, 0.898f, 0.82f),
			};
		pickButton.Pressed += () => { if (activePick?.CanConfirm == true) EmitSignal(SignalName.PickConfirmed); };

		// The fan looks the same as the passive hand, so say in words that it is waiting on a click.
		pickHint = new Label
		{
			Name = "PickHint",
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			LabelSettings = new LabelSettings
			{
				Font = GD.Load<Font>("res://scenes/menu/theme/MenuFontBody.tres"),
				FontSize = 15,
				FontColor = new Color(0.98f, 0.86f, 0.55f),
				OutlineSize = 6,
				OutlineColor = new Color(0f, 0f, 0f, 0.85f),
			},
		};
		pickButton.AddChild(pickHint);
	}

	private static StyleBoxFlat PickButtonStyle(Color background, Color border)
	{
		StyleBoxFlat style = new() { BgColor = background, BorderColor = border };
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(3);
		return style;
	}

	/// <summary>Shows, labels and places the confirm button for the open pick, or hides it when there is none.</summary>
	private void UpdatePickButton()
	{
		if (pickButton == null) return;
		pickButton.Visible = activePick != null;
		if (activePick == null) return;

		pickButton.ButtonText = activePick.ConfirmLabel;
		pickHint.Text = activePick.Selected.Count == 0
			? "Click cards in your hand to select them"
			: "Click a selected card to put it back";
		pickHint.Size = new Vector2(pickButton.Size.X + 200f, 24f);
		pickHint.Position = new Vector2(-100f, -30f);
		pickButton.Disabled = !activePick.CanConfirm;
		// Placed from the real size, so whatever the label needs the button still ends above the raised cards.
		pickButton.Size = pickButton.GetCombinedMinimumSize();
		Vector2 size = pickButton.Size;
		pickButton.Position = new Vector2(fanCentreX - size.X * 0.5f, fanTopY - PickRise - PickButtonGap - size.Y);
	}

	/// <summary>
	/// Tears the fan down. A card whose key is in <paramref name="keptKeys"/> is freed at once and its
	/// pose remembered, so its replacement can slide from there; every other card fades out and sinks.
	/// </summary>
	private void DeleteCurrentCards(HashSet<string> keptKeys)
	{
		// The nodes about to be freed are the only things that could ever fire the MouseExited that
		// clears a hover preview, so a glow raised from one of them would otherwise be permanent.
		CardTargetPreviewDisplay.ClearAll();
		previousPoses.Clear();
		anyLeaving = false;

		foreach (CardScene cardScene in CardScenes)
		{
			cardScene.Selected -= OnCardSelected;
			if (cardTweens.Remove(cardScene, out Tween running)) running.Kill();
			cardKeys.Remove(cardScene, out string key);
			restPositions.Remove(cardScene);

			bool kept = key != null && keptKeys.Contains(key);
			if (kept)
				previousPoses[key] = new CardPose(cardScene.Position, cardScene.Rotation, cardScene.Scale, cardScene.Modulate.A);

			if (kept || CardsContainer == null || !cardScene.IsInsideTree())
			{
				cardScene.GetParent()?.RemoveChild(cardScene);
				cardScene.QueueFree();
				continue;
			}

			// Leaving: dead to the mouse straight away, drawn under the incoming fan until it is gone.
			anyLeaving = true;
			cardScene.SetMousePassthrough(true);
			cardScene.SetClickable(false);
			cardScene.ZIndex -= 100;
			Tween leave = cardScene.CreateTween().SetParallel().SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
			leave.TweenProperty(cardScene, "modulate:a", 0f, LeaveSeconds);
			leave.TweenProperty(cardScene, "position:y", cardScene.Position.Y + LeaveDrop, LeaveSeconds);
			leave.Chain().TweenCallback(Callable.From(cardScene.QueueFree));
		}
		CardScenes.Clear();
	}

	/// <summary>
	/// Animates a freshly laid-out card into its final pose: from its old pose if it was already on
	/// show, otherwise rising from below and fading in, after the leaving cards have begun to clear.
	/// </summary>
	private void AnimateIn(CardScene card, string key)
	{
		if (!card.IsInsideTree()) return;
		Vector2 position = card.Position;
		float rotation = card.Rotation;
		Vector2 scale = card.Scale;
		Tween tween = card.CreateTween().SetParallel().SetEase(Tween.EaseType.Out);
		cardTweens[card] = tween;

		if (previousPoses.Remove(key, out CardPose from))
		{
			card.Position = from.Position;
			card.Rotation = from.Rotation;
			card.Scale = from.Scale;
			card.Modulate = card.Modulate with { A = from.Alpha };
			tween.SetTrans(Tween.TransitionType.Cubic);
			tween.TweenProperty(card, "position", position, SlideSeconds);
			tween.TweenProperty(card, "rotation", rotation, SlideSeconds);
			tween.TweenProperty(card, "scale", scale, SlideSeconds);
			tween.TweenProperty(card, "modulate:a", 1f, SlideSeconds);
		}
		else
		{
			float delay = (anyLeaving ? LeaveSeconds * 0.5f : 0f) + enterOrder++ * EnterStagger;
			card.Position = position + new Vector2(0, EnterRise);
			card.Modulate = card.Modulate with { A = 0f };
			tween.TweenProperty(card, "position", position, EnterSeconds).SetDelay(delay).SetTrans(Tween.TransitionType.Back);
			tween.TweenProperty(card, "modulate:a", 1f, EnterSeconds * 0.6f).SetDelay(delay).SetTrans(Tween.TransitionType.Cubic);
		}
		tween.Finished += () => cardTweens.Remove(card);
	}

	public void ShowCardEmphasis(int cardId)
	{        
		// Only access CardPreview if it's been initialized (in LoadUI)
		if (CardPreview != null)
		{
			CardPreview.ShowCard(cardId);
		}
	}

	/// <summary>The enlarged preview for a face with no card behind it, such as an option card.</summary>
	public void ShowFaceEmphasis(CardFace face) => CardPreview?.ShowFace(face);

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
