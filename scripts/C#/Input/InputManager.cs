using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class InputManager : Node2D
{
    public static InputManager Current;
    private static Godot.Vector2 DEFAULT_POSITION = new Godot.Vector2(6321,1584);
    private static Godot.Vector2 DEFAULT_ZOOM = new Godot.Vector2(0.15f,0.15f);
    private const float ZOOM_STEP = 0.05f;
    private const float CAMERA_SPEED = 10f;
    private const float MIN_ZOOM_LEVEL = 0.1f;
    private const float MAX_ZOOM_LEVEL = 2;    
    private float zoom = 0.2f;
    public Camera2D Camera => GetNode<Camera2D>("%Camera2D"); 

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
    public InputHandlerPlayCard SetCardSelectionActive(
        Faction faction, List<int> cardIds, bool isReactionWindow = false, List<int> displayCardIds = null)
    {
        _pendingReactionSkipScope = ReactionSkipScope.NONE;

        CurrentCardPrompt = new ActiveCardPrompt(faction, displayCardIds ?? cardIds, cardIds);

        PlayerActionLabel.ShowText(cardIds.Count > 0 ? "Choose a card" : "No reaction available", faction);
        // The faction is passed explicitly: the one-argument Show overload reads it off cardIds[0]
        // and would resolve Faction.NONE for an empty always-ask prompt.
        FactionHandDisplay.Current.Show(displayCardIds ?? cardIds, faction, cardIds);
        FactionHandDisplay.Current.CardSelected += HandleItemSelected;
        EventBus.Emit(EventBus.SignalName.CardPromptOpened, (int)faction);
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
            CurrentCardPrompt.DisplayCardIds, CurrentCardPrompt.Faction, CurrentCardPrompt.SelectableCardIds);
        return true;
    }

    private void HandleItemSelected(int cardId)
    {
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
        EventBus.Emit("CardSelected", cardId);
    }

    public override void _Ready()
    {
        if(Multiplayer.GetUniqueId() == GetMultiplayerAuthority())
        {
            Current = this;
            Camera.Enabled = true;
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
        if (zoom > MIN_ZOOM_LEVEL)
        {
            zoom -= ZOOM_STEP;
            ApplyZoom();
        }
    }

    private void ApplyZoom()
    {
        if (Camera == null) return;
        var pos = Camera.Position;
        Camera.Zoom = new Vector2(zoom, zoom);
    }
}
