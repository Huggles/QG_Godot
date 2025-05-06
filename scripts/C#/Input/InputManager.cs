using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class InputManager : Node3D
{
    private static readonly Vector3 INITIAL_POSITION = new Vector3(0, 0, 150);
    private const float ZOOM_STEP = 5f;
    private const float CAMERA_SPEED = 0.5f;
    private const float MIN_ZOOM_LEVEL = -10f;
    private const float MAX_ZOOM_LEVEL = 10f;
    private static readonly Key[] HAND_CARD_KEYS = {
        Key.Key0, Key.Key1, Key.Key2, Key.Key3, Key.Key4, Key.Key5, Key.Key6
    };

    private int _zoomLevel = 0;
    public Camera3D Camera => GetNode<Camera3D>("%Camera3D"); 

    public Vector2 MousePosition => GetViewport().GetMousePosition();

    private RayTraceCaster _rayTraceCaster;

    private List<InputEventMouseButton> _currentlyPressedMouseButtons = new();
    private List<InputEventMouseButton> _previouslyPressedMouseButtons = new();

    private List<InputEventKey> _currentlyPressedKeyboardButtons = new();
    private List<InputEventKey> _previouslyPressedKeyboardButtons = new();

    private IInputHandler inputHandler;

    [Signal]
    public delegate void KeyClickedEventHandler(InputEventKey keyEvent);

    public IInputHandler SetPlayCardInputActive()
    {
        inputHandler = new InputHandlerPlayCard();
        return inputHandler;
    }

    // public InputHandlerActivateCard SetActivateActionInputActive(Enum.Faction faction)
    // {
    //     _inputHandler = new InputHandlerActivateCard(faction);
    //     return (InputHandlerActivateCard)_inputHandler;
    // }

    // public InputHandlerActivateCard SetActivateBeforeActionInputActive(Enum.Faction faction, GameChangeEvent gce)
    // {
    //     _inputHandler = new InputHandlerActivateBeforeCard(faction, gce);
    //     return (InputHandlerActivateCard)_inputHandler;
    // }

    // public InputHandlerDiscardCard SetDiscardInputActive()
    // {
    //     _inputHandler = new InputHandlerDiscardCard();
    //     return (InputHandlerDiscardCard)_inputHandler;
    // }

    // public InputHandlerDebug SetDebugInputActive()
    // {
    //     _inputHandler = new InputHandlerDebug();
    //     return (InputHandlerDebug)_inputHandler;
    // }

    public void SetNoInputActive()
    {
        inputHandler = null;
    }

    public override void _EnterTree()
    {
        // Called before _Ready() in Godot C#
        // EventBusLocal.SetUnitsClickable += _ => EnableRayTraceCasting();
        // EventBusLocal.SetCountriesClickable += _ => EnableRayTraceCasting();
        // EventBusLocal.SetAllUnitsUnclickable += DisableRayTraceCasting;
        // EventBusLocal.SetAllCountriesUnclickable += DisableRayTraceCasting;

        KeyClicked += HandleKeyClicked;
        Camera.Position = INITIAL_POSITION;
    }

    public override void _Process(double delta)
    {
        KeyboardMovement();

        if (_rayTraceCaster != null)
        {
            _rayTraceCaster.CastRays(null);
        }
    }

    private void KeyboardMovement()
    {
        if (Camera == null) return;

        int inputUp = Input.IsActionPressed("ui_up") ? 1 : 0;
        int inputDown = Input.IsActionPressed("ui_down") ? 1 : 0;
        int inputLeft = Input.IsActionPressed("ui_left") ? 1 : 0;
        int inputRight = Input.IsActionPressed("ui_right") ? 1 : 0;

        float zoomMultiplier = 5 - _zoomLevel;
        float xDelta = (-inputLeft + inputRight) * CAMERA_SPEED;
        float yDelta = (inputUp - inputDown) * CAMERA_SPEED;

        Vector3 delta = new Vector3(xDelta, yDelta, 0) * Mathf.Clamp(zoomMultiplier, 1, 5);
        Camera.Position += delta;
    }

    public override void _Input(InputEvent e)
    {
        HandleInput(e);
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
            _rayTraceCaster?.CastRays(e);

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

    private void HandleKeyClicked(InputEventKey keyEvent)
    {
        //_inputHandler?.OnKeyClicked(keyEvent);
    }

    private void EnableRayTraceCasting()
    {
        _rayTraceCaster ??= new RayTraceCaster(this);
    }

    private void DisableRayTraceCasting()
    {
        _rayTraceCaster = null;
    }

    private void ZoomIn()
    {
        if (_zoomLevel > MIN_ZOOM_LEVEL)
        {
            _zoomLevel--;
            ApplyZoom();
        }
    }

    private void ZoomOut()
    {
        if (_zoomLevel < MAX_ZOOM_LEVEL)
        {
            _zoomLevel++;
            ApplyZoom();
        }
    }

    private void ApplyZoom()
    {
        if (Camera == null) return;
        var pos = Camera.Position;
        pos.Z = INITIAL_POSITION.Z + (_zoomLevel * ZOOM_STEP);
        Camera.Position = pos;
    }
}
