using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class InputManager : Node2D
{
	public static InputManager Current;
	private static Godot.Vector2 DEFAULT_POSITION = new Godot.Vector2(6321,1584);
	private static Godot.Vector2 DEFAULT_ZOOM = new Godot.Vector2(0.15f,0.15f);
	private const float ZOOM_STEP = 0.01f;
	private const float CAMERA_SPEED = 10f;
	private const float MIN_ZOOM_LEVEL = 0.1f;
	private const float MAX_ZOOM_LEVEL = 2;    
	private float zoom = 0.2f;
	public Camera2D Camera => GetNode<Camera2D>("%MainGameCamera"); 

	public Vector2 MousePosition => GetViewport().GetMousePosition();

	private RayTraceCaster _rayTraceCaster;

	private List<InputEventMouseButton> _currentlyPressedMouseButtons = new();
	private List<InputEventMouseButton> _previouslyPressedMouseButtons = new();

	private List<InputEventKey> _currentlyPressedKeyboardButtons = new();
	private List<InputEventKey> _previouslyPressedKeyboardButtons = new();

	private InputHandlerPlayCard inputHandler;

	/// <summary>Scope chosen on the open reaction prompt, consumed by <see cref="TakeReactionSkipScope"/>.</summary>
	private ReactionSkipScope _pendingReactionSkipScope = ReactionSkipScope.NONE;

	/// <summary>
	/// What the open card prompt is offering, or null when no prompt is open on this peer. The
	/// request object itself never reaches the client — <see cref="InputRequest.Execute"/> pushes
	/// into the UI singletons and is gone — so this is the only place the prompt survives after
	/// <see cref="FactionHandDisplay"/> has been drawn over by something else.
	/// </summary>
	public static ActiveCardPrompt CurrentCardPrompt { get; private set; }

	[Signal] public delegate void KeyClickedEventHandler(InputEventKey keyEvent);

	/// <summary>
	/// Activates card selection over an explicit set of card ids. Every card prompt goes through here
	/// with the list the host computed and put on the wire as InputRequest.TargetCardIds — a client
	/// runs no GameStateCalculator and holds no CardPlayRound, so it cannot re-derive an option set
	/// that depends on either (reaction depth being the one that bit: re-deriving inside a reaction
	/// window offered the faction's whole hand).
	/// </summary>
	/// <param name="isReactionWindow">
	/// True for an after-reaction or block prompt, which additionally offers the scoped skip buttons.
	/// Such a prompt may legitimately carry an empty <paramref name="cardIds"/>: a faction with a
	/// face-down Response card is asked in every window whether or not it can actually react, so
	/// that the prompt itself stops giving the hidden card away.
	/// </param>
	/// <param name="displayCardIds">
	/// What to draw, when that is wider than what may be chosen — a reaction window shows the
	/// faction's whole event-triggered table and greys out everything not in
	/// <paramref name="cardIds"/>. Null means "draw exactly the selectable set".
	/// </param>
	/// <param name="separateNonHandCards">
	/// True for the hand-play prompt, the one prompt whose offer spans two zones: the cards that are
	/// not in hand get their own smaller fan beside the hand instead of being interleaved with it by
	/// card id. See <see cref="FactionHandDisplay.Show(List{int}, Faction, List{int}, bool)"/> — it
	/// must stay false for the table-cards-only prompts, which would end up with an empty hand.
	/// </param>
	/// <param name="cardTargetPreviews">
	/// The host's per-card target preview, straight off <see cref="InputRequest.CardTargetPreviews"/>.
	/// Lights the targeted countries and units when the player hovers a card — see
	/// <see cref="CardTargetPreviewDisplay"/>.
	/// Null is fine and means no card previews anything.
	/// </param>
	/// <param name="triggerKind">
	/// Which window this prompt is, straight off <see cref="InputRequest.TriggerReactionKind"/> — it
	/// only decides the banner wording. Kept separate from <paramref name="isReactionWindow"/>, which
	/// decides whether the scoped skip buttons appear: a block and an after window agree on that and
	/// differ here.
	/// </param>
	public InputHandlerPlayCard SetCardSelectionActive(
		Faction faction, List<int> cardIds, bool isReactionWindow = false, List<int> displayCardIds = null,
		bool separateNonHandCards = false, List<InputRequest.CardTargetPreview> cardTargetPreviews = null,
		TriggerKind triggerKind = TriggerKind.NONE)
	{
		_pendingReactionSkipScope = ReactionSkipScope.NONE;

		// A previous prompt's glow must never survive into this one. SetCardSelectionActive is also
		// reached without HandleItemSelected having run in between (a retried request opens a fresh
		// prompt over the old one), so clearing here rather than only on teardown.
		CardTargetPreviewDisplay.ClearAll();

		CurrentCardPrompt = new ActiveCardPrompt(
			faction, displayCardIds ?? cardIds, cardIds, separateNonHandCards,
			ToPreviewMap(cardTargetPreviews));

		PlayerActionLabel.ShowText(BannerText(triggerKind, cardIds.Count > 0), faction);
		// The faction is passed explicitly: the one-argument Show overload reads it off cardIds[0]
		// and would resolve Faction.NONE for an empty always-ask prompt.
		FactionHandDisplay.Current.Show(displayCardIds ?? cardIds, faction, cardIds, separateNonHandCards);
		FactionHandDisplay.Current.CardSelected += HandleItemSelected;
		EventBus.Emit(EventBus.SignalName.CardPromptOpened, (int)faction);
		// A card prompt is recallable for as long as it is open: the player can browse another faction's
		// hand over the top of it, and the recall button draws this one back.
		RecallablePrompts.Set(CardPromptRecall.Instance);
		// The Skip button now doubles as the "pass" affordance for choosing a card to play/activate.
		SelectionSkipButton.Current?.Show();
		EventBus.Instance.SelectionSkipped += OnPlayCardSkipped;

		if (isReactionWindow)
		{
			ReactionSkipScopeButton.ShowAll();
			EventBus.Instance.ReactionSkipScoped += OnReactionSkipScoped;
		}

		return inputHandler;
	}

	/// <summary>
	/// What the banner over the hand says. Names the window the player is in, because "Choose a card"
	/// and "No reaction available" read the same whether the prompt is the one chance to stop an event
	/// or the chance to answer one that already happened — and an empty always-ask window is exactly
	/// where a player most needs to know which.
	///
	/// The trigger context panel says the same thing at more length; this is the line at the hand,
	/// where the player is already looking to pick a card.
	/// </summary>
	private static string BannerText(TriggerKind kind, bool hasOptions) => kind switch
	{
		TriggerKind.BLOCK => hasOptions ? "Choose a block reaction" : "No block available",
		TriggerKind.AFTER => hasOptions ? "Choose an after reaction" : "No reaction available",
		_                 => hasOptions ? "Choose a card" : "No reaction available",
	};

	/// <summary>
	/// Flattens the wire list into the by-card-id lookup the hover path wants. Defensive against
	/// duplicate card ids: the host builds the list with Distinct(), but ToDictionary would throw on
	/// the prompt rather than merely mis-drawing a preview if that ever changed.
	/// </summary>
	private static Dictionary<int, InputRequest.CardTargetPreview> ToPreviewMap(List<InputRequest.CardTargetPreview> previews)
	{
		if (previews == null) return null;

		Dictionary<int, InputRequest.CardTargetPreview> map = new();
		foreach (InputRequest.CardTargetPreview preview in previews)
			map[preview.CardId] = preview;
		return map;
	}

	private void OnPlayCardSkipped()
	{
		// Skipping the card-play prompt is a pass (card id -1).
		HandleItemSelected(-1);
	}

	/// <summary>
	/// A scoped skip is still a pass on this window; the scope is what the host reads afterwards to
	/// decide how long to leave this faction alone.
	/// </summary>
	private void OnReactionSkipScoped(int scope)
	{
		_pendingReactionSkipScope = (ReactionSkipScope)scope;
		HandleItemSelected(-1);
	}

	/// <summary>
	/// Read and clear the scope chosen for the prompt that just closed. Read once, by the request's
	/// Handle() right after the card selection resolves, so a stale scope cannot leak into the next
	/// prompt (SetCardSelectionActive resets it on entry as a second guard).
	/// </summary>
	public ReactionSkipScope TakeReactionSkipScope()
	{
		ReactionSkipScope scope = _pendingReactionSkipScope;
		_pendingReactionSkipScope = ReactionSkipScope.NONE;
		return scope;
	}

	/// <summary>
	/// Release an active card-selection prompt as a pass without the player clicking anything. Registered
	/// with <see cref="PendingLocalInput"/> by the requests that await the CardSelected signal, so the
	/// host abandoning a request (input timeout, error recovery) tears the prompt down through exactly
	/// the same path as the Skip button — including emitting CardSelected, which is what releases the
	/// awaiting handler. Without it those three prompts stayed live and clickable on a dead request.
	/// </summary>
	public void CancelCardSelection() => HandleItemSelected(-1);

	/// <summary>
	/// Re-draw the open card prompt over whatever is on the hand display now — the way back from
	/// browsing another faction's hand. A no-op returning false when no prompt is open. The
	/// CardSelected subscription lives on the display node rather than on the card nodes, so
	/// re-showing does not disturb the handler that is awaiting the answer.
	/// </summary>
	public static bool ShowCurrentCardPrompt()
	{
		if (CurrentCardPrompt == null || FactionHandDisplay.Current == null)
		{
			return false;
		}

		FactionHandDisplay.Current.Show(
			CurrentCardPrompt.DisplayCardIds, CurrentCardPrompt.Faction, CurrentCardPrompt.SelectableCardIds,
			CurrentCardPrompt.SeparateNonHandCards);
		return true;
	}

	private void HandleItemSelected(int cardId)
	{
		// The prompt is answered, so its hover glow must go: nothing else clears it on this path, and a
		// country left lit would stay lit through the country-selection prompt that follows and beyond.
		CardTargetPreviewDisplay.ClearAll();
		CurrentCardPrompt = null;
		FactionHandDisplay.Current.CardSelected -= HandleItemSelected;
		EventBus.Instance.SelectionSkipped -= OnPlayCardSkipped;
		// Unconditional: the host's abort path (CancelCardSelection on timeout or error recovery)
		// comes through here too, and an unsubscribe/hide that never ran would leave the scoped
		// buttons live over the next prompt. Both are no-ops when they were never set up.
		EventBus.Instance.ReactionSkipScoped -= OnReactionSkipScoped;
		ReactionSkipScopeButton.HideAll();
		SelectionSkipButton.Current?.Hide();
		FactionHandDisplay.Current.Hide();
		// The trigger context is its own node now, so hiding the hand no longer takes it down with it.
		TriggerContextDisplay.Current?.Hide();
		EventBus.Emit(EventBus.SignalName.CardPromptClosed);
		RecallablePrompts.Clear(CardPromptRecall.Instance);
		EventBus.Emit("CardSelected", cardId);
	}

	public override void _Ready()
	{
		if(Multiplayer.GetUniqueId() == GetMultiplayerAuthority())
		{
			Current = this;
			Camera.Enabled = true;
			// ApplyZoom re-frames through ApplyCameraBounds, so this also pulls the blind
			// DEFAULT_POSITION/DEFAULT_ZOOM from _EnterTree onto the board.
			ApplyZoom();
		}
		else
		{
			// Remote player's camera must be disabled so only the local
			// player's camera renders the viewport.
			Camera.Enabled = false;
		}
	}

	public override void _EnterTree()
	{
		Camera.Position = DEFAULT_POSITION;
		Camera.Zoom = DEFAULT_ZOOM;
		zoom = DEFAULT_ZOOM.X;
	}

	public override void _Process(double delta)
	{
		KeyboardMovement();
		ApplyCameraBounds();
	}

	/// <summary>
	/// Clamps the camera's limits, zoom range and position to the board backdrop
	/// (<see cref="NodeUtilities.BoardBounds"/>), so the viewport can never show anything outside the
	/// NinePatchRect. Re-derived every frame rather than cached once: it costs one transform multiply,
	/// and it means moving or resizing the NinePatchRect — in the editor or at runtime — retunes the
	/// camera with no further wiring. A no-op while the board is not in the tree (menu, lobby,
	/// headless), which is why the call in <see cref="_Ready"/> is not enough on its own.
	///
	/// Position is clamped here rather than left to Camera2D's own limits: those clamp the rendered
	/// transform but leave <c>Camera.Position</c> wherever it was written, so panning or a
	/// <see cref="ZoomToCountryAnimation"/> tween aimed off-board would park the node out there and
	/// the next keypress would appear to do nothing.
	/// </summary>
	private void ApplyCameraBounds()
	{

		Vector2 margin = new Vector2(1500,1500);
		if (Camera == null) return;

		Rect2? bounds = NodeUtilities.Instance?.BoardBounds;
		if (bounds == null) return;

		Rect2 board = bounds.Value;
		Camera.LimitLeft = Mathf.RoundToInt(board.Position.X) - Mathf.RoundToInt(margin.X);
		Camera.LimitTop = Mathf.RoundToInt(board.Position.Y) - Mathf.RoundToInt(margin.Y);
		Camera.LimitRight = Mathf.RoundToInt(board.End.X) + Mathf.RoundToInt(margin.X);
		Camera.LimitBottom = Mathf.RoundToInt(board.End.Y) + Mathf.RoundToInt(margin.Y);

		// AnchorMode is the default DragCenter, so Position is the centre of the view and the legal
		// centres are the board inset by half a viewport. MinZoomLevel already keeps that half-extent
		// under half the board, but Max guards the degenerate case anyway — an inverted range would
		// otherwise snap the camera to the far edge.
		Vector2 halfExtent = GetViewport().GetVisibleRect().Size / (2f * Camera.Zoom);
		Vector2 min = board.Position + halfExtent;
		min -= margin;

		Vector2 max = board.End - halfExtent;
		max += margin;

		Camera.Position = new Vector2(
			Mathf.Clamp(Camera.Position.X, min.X, Mathf.Max(min.X, max.X)),
			Mathf.Clamp(Camera.Position.Y, min.Y, Mathf.Max(min.Y, max.Y)));
	}

	/// <summary>
	/// The furthest out the player may zoom: whatever still keeps the whole viewport inside the board,
	/// falling back to <see cref="MIN_ZOOM_LEVEL"/> when there is no board to measure against.
	/// </summary>
	private float MinZoomLevel
	{
		get
		{
			Rect2? bounds = NodeUtilities.Instance?.BoardBounds;
			if (bounds == null || bounds.Value.Size.X <= 0 || bounds.Value.Size.Y <= 0)
				return MIN_ZOOM_LEVEL;

			Vector2 viewport = GetViewport().GetVisibleRect().Size;
			float fitZoom = Mathf.Max(viewport.X / bounds.Value.Size.X, viewport.Y / bounds.Value.Size.Y);
			return Mathf.Max(MIN_ZOOM_LEVEL, fitZoom);
		}
	}

	private void KeyboardMovement()
	{
		if (Camera == null) return;

		int inputUp = Input.IsActionPressed("ui_up") ? 1 : 0;
		int inputDown = Input.IsActionPressed("ui_down") ? 1 : 0;
		int inputLeft = Input.IsActionPressed("ui_left") ? 1 : 0;
		int inputRight = Input.IsActionPressed("ui_right") ? 1 : 0;

		float zoomMultiplier = Mathf.Clamp(10 - zoom, 1, 10);
		float xDelta = (-inputLeft + inputRight) * CAMERA_SPEED;
		float yDelta = (-inputUp + inputDown) * CAMERA_SPEED;
		Vector2 delta = new Vector2(xDelta, yDelta) * zoomMultiplier;
		Camera.Position += delta;
	}

	public override void _UnhandledInput(InputEvent e)
	{
		HandleInput(e);
	}

	private void HandleInput(InputEvent e)
	{
		if (e is InputEventMouse mouseEvent)
		{
			HandleMouseInput(mouseEvent);
		}
		else if (e is InputEventKey keyEvent)
		{
			HandleKeyboardInput(keyEvent);
		}
	}

	private void HandleMouseInput(InputEventMouse e)
	{
		if (e is InputEventMouseButton)
		{
			if (Input.IsActionPressed("game_zoom_in"))
				ZoomIn();
			if (Input.IsActionPressed("game_zoom_out"))
				ZoomOut();
		}
	}

	private void HandleKeyboardInput(InputEventKey keyEvent)
	{
		_previouslyPressedKeyboardButtons = new(_currentlyPressedKeyboardButtons);
		_currentlyPressedKeyboardButtons.Clear();        

		if (keyEvent.IsPressed())
		{
			_currentlyPressedKeyboardButtons.Add(keyEvent);
		}

		foreach (var prev in _previouslyPressedKeyboardButtons)
		{
			bool stillPressed = _currentlyPressedKeyboardButtons.Any(curr => curr.Keycode == prev.Keycode);
			if (!stillPressed)
			{
				EmitSignal(SignalName.KeyClicked, keyEvent);
			}
		}
	}

	public void EnableRayTraceCasting()
	{
		_rayTraceCaster ??= new RayTraceCaster(this);
	}

	public void DisableRayTraceCasting()
	{
		_rayTraceCaster = null;
	}

	private void ZoomIn()
	{
		if (zoom < MAX_ZOOM_LEVEL)
		{
			zoom += ZOOM_STEP;
			ApplyZoom();
		}
		
	}

	private void ZoomOut()
	{
		if (zoom > MinZoomLevel)
		{
			zoom -= ZOOM_STEP;
			ApplyZoom();
		}
	}

	private void ApplyZoom()
	{
		if (Camera == null) return;
		// The floor is the board-fit zoom, not MIN_ZOOM_LEVEL: DEFAULT_ZOOM is set blind in
		// _EnterTree and a small board would make it show past the edges.
		zoom = Mathf.Clamp(zoom, MinZoomLevel, MAX_ZOOM_LEVEL);
		Camera.Zoom = new Vector2(zoom, zoom);
		ApplyCameraBounds();
	}
}
