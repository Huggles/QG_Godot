using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class InputManager : Node2D
{
    public static InputManager Instance;
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

    [Signal]
    public delegate void KeyClickedEventHandler(InputEventKey keyEvent);

    public InputHandlerPlayCard SetPlayCardInputActive(List<CardActivationOption> cardActivationOptions)
    {        
        inputHandler = new InputHandlerPlayCard(cardActivationOptions);
        return inputHandler;
    }

    public override void _Ready()
    {
        Instance = this;
        ApplyZoom();
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
