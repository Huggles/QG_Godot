using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class PresentationModal : Control, LoadableUI
{
    public static PresentationModal Instance;
    public RichTextLabel TitleText;
    public PanelContainer PanelContainer;
    public GridContainer GridCardContainer;
    public ScrollContainer CardScrollContainer;

    public bool RequireSelection;
    public Button ExitButton;
    public Button ConfirmButton;

    // Multi-select support
    private bool multiSelectMode = false;
    private int minimumSelections = 0;
    private List<int> selectedIdentifiers = new List<int>();
    private Action<List<int>> confirmCallback;
    private Action skipCallback;
    
    public List<PresentationItem> PresentationItems = new List<PresentationItem>();
    public List<Control> PresentationItemControls = new List<Control>();

    // Event handlers for cleanup
    private Action onConfirmButtonPressed;
    private Action onExitButtonPressed;
    private InputManager.KeyClickedEventHandler onKeyClicked;

    [Signal] public delegate void OnShowEventHandler();
    [Signal] public delegate void OnHideEventHandler();
    [Signal] public delegate void ItemSelectedEventHandler(int identifier);


    public override void _Ready()
    {
        Instance = this;
        
        // Hide by default until LoadUI is called
        Hide();
        
        EventBus.Emit(EventBus.SignalName.UserInterfaceLoaded, "PresentationModal");
    }

    public override void _ExitTree()
    {
        UnsubscribeFromEvents();
    }

    public void LoadUI()
    {
        TitleText = GetNode<RichTextLabel>("%TitleText");
        PanelContainer = GetNode<PanelContainer>("%PanelContainer");
        GridCardContainer = GetNode<GridContainer>("%GridCardContainer");
        ExitButton = GetNode<Button>("%ExitButton");
        CardScrollContainer = GetNode<ScrollContainer>("%CardScrollContainer");
        
        // Unsubscribe first to prevent duplicate connections
        UnsubscribeFromEvents();
        
        // Try to get ConfirmButton if it exists in the scene
        if (HasNode("%ConfirmButton"))
        {
            ConfirmButton = GetNode<Button>("%ConfirmButton");
            onConfirmButtonPressed = HandleConfirmPressed;
            ConfirmButton.Pressed += onConfirmButtonPressed;
        }

        Visible = false;
        Modulate = new Color(1, 1, 1, 0);
        ExitButton.Visible = false;
        if (ConfirmButton != null)
            ConfirmButton.Visible = false;
    }
    public SignalAwaiter ShowModal(List<PresentationItem> presentationItems, string title, bool requireSelection)
    {
        RequireSelection = requireSelection;
        return ShowModal(presentationItems, title, null);
    }

    public SignalAwaiter ShowModal(List<PresentationItem> presentationItems, string title, Action exitCallback)
    {
        if (!RequireSelection)
        {
            ShowExitButton(exitCallback != null ? exitCallback : () => { this.HideModal(); });
            
            // Unsubscribe first to prevent duplicate connections
            if (onKeyClicked != null)
            {
                InputManager.Instance.KeyClicked -= onKeyClicked;
            }
            onKeyClicked = HandleKeyboardInput;
            InputManager.Instance.KeyClicked += onKeyClicked;
        }        
        ShowModalPersistent(presentationItems, title);        
        return ToSignal(this, SignalName.ItemSelected);
    }

    public SignalAwaiter ShowModal(List<PresentationItem> presentationItems, string title)
    {
        HandlePresentationItems(presentationItems);
        TitleText.Text = title;
        Visible = true;

        var tween1 = GetTree().CreateTween();
        PropertyTweener propertyTweener1 = tween1.TweenProperty(this, "modulate:a", 1, GameSettings.AnimationDurationSeconds);
        propertyTweener1.Finished += async () =>
        {
            DebugUtilities.PrintPeer("PresentationModal shown", DebugVerbosity.INFO);
            EmitSignal(SignalName.OnShow);
            await Task.Delay(GameSettings.PauseDuration);
            tween1.Dispose();

            if (GameSettings.AnimationDurationSeconds > 0)
            {
                _ = HideModal();
            }
        };
        return ToSignal(this, SignalName.OnHide);        
    }

    public SignalAwaiter ShowModalPersistent(List<PresentationItem> presentationItems, string title)
    {
        HandlePresentationItems(presentationItems);
        TitleText.Text = title;
        Visible = true;

        var tween1 = GetTree().CreateTween();
        PropertyTweener propertyTweener1 = tween1.TweenProperty(this, "modulate:a", 1, GameSettings.AnimationDurationSeconds);
        
        propertyTweener1.Finished += async () =>
        {   
            tween1.Dispose();
            EmitSignal(SignalName.OnShow);
        };
        return ToSignal(this, SignalName.OnShow);        
    }

    public void ShowExitButton(Action callback)
    {
        ExitButton.Visible = true;
        
        // Unsubscribe previous handler if exists
        if (onExitButtonPressed != null)
        {
            ExitButton.Pressed -= onExitButtonPressed;
        }
        
        onExitButtonPressed = () =>
        {
            callback.Invoke();
        };
        ExitButton.Pressed += onExitButtonPressed;
    }

    private void HandlePresentationItems(List<PresentationItem> presentationItems)
    {
        int maxColumns = 7;
        if (presentationItems != null && presentationItems.Count > 0)
        {
            PresentationItems = presentationItems;
            
            // Set columns to at most 7 per row
            GridCardContainer.Columns = Math.Min(presentationItems.Count, maxColumns);            
            int rows = (int)Math.Ceiling((double)presentationItems.Count / maxColumns);
            
            foreach (PresentationItem presentationItem in presentationItems)
            {
                Control control = presentationItem.InitializeControl();
                GridCardContainer.AddChild(control);

                PresentationItemControls.Add(control);
                presentationItem.LoadControl();
                
                // Connect to appropriate handler based on mode
                if (multiSelectMode)
                {
                    presentationItem.ItemClicked += HandleItemClickedMultiSelect;
                    // Initialize all cards as unselected (darker)
                    control.Modulate = new Color(0.5f, 0.5f, 0.5f);
                }
                else
                {
                    presentationItem.ItemClicked += HandleItemClicked;
                }
            }
            
            // Set grid size to fit one row of cards
            Vector2 gridSize = new Vector2(0, PresentationItemControls[0].Size.Y * Mathf.Clamp(rows, 1, 2.1f));
            CardScrollContainer.CustomMinimumSize = gridSize;
            CardScrollContainer.Size = gridSize;
            CardScrollContainer.ScrollVertical = 0;
        }
    }
    private void HandleItemClicked(int identifier) {        
        EmitSignal(SignalName.ItemSelected, identifier);
    }

    private void HandleKeyboardInput(InputEventKey inputEventKey)
    {
        if (inputEventKey.Keycode == Key.Escape)
        {
            HideModal();
        }
    }

    // Multi-select functionality
    public void ShowModalMultiSelect(List<PresentationItem> presentationItems, string title, int minimumSelections, Action<List<int>> onConfirm, Action onSkip)
    {
        this.multiSelectMode = true;
        this.minimumSelections = minimumSelections;
        this.confirmCallback = onConfirm;
        this.skipCallback = onSkip;
        this.selectedIdentifiers.Clear();

        HandlePresentationItems(presentationItems);
        TitleText.Text = title;
        Visible = true;

        // Show buttons
        if (ConfirmButton != null)
        {
            ConfirmButton.Visible = true;
            UpdateConfirmButton();
        }
        
        if (minimumSelections == 0 && skipCallback != null)
        {
            ShowExitButton(() => 
            { 
                skipCallback?.Invoke();
            });
        }

        var tween1 = GetTree().CreateTween();
        PropertyTweener propertyTweener1 = tween1.TweenProperty(this, "modulate:a", 1, GameSettings.AnimationDurationSeconds);
        propertyTweener1.Finished += () =>
        {
            DebugUtilities.PrintPeer("PresentationModal shown", DebugVerbosity.INFO);
            EmitSignal(SignalName.OnShow);
            tween1.Dispose();
        };
    }

    private void HandleItemClickedMultiSelect(int identifier)
    {
        if (selectedIdentifiers.Contains(identifier))
        {
            selectedIdentifiers.Remove(identifier);
            UpdateItemVisual(identifier, false);
        }
        else
        {
            selectedIdentifiers.Add(identifier);
            UpdateItemVisual(identifier, true);
        }
        
        UpdateConfirmButton();
    }

    private void UpdateItemVisual(int identifier, bool selected)
    {
        PresentationItem item = PresentationItems.Find(p => p.Identifier == identifier);
        if (item?.Control != null)
        {
            // Visual feedback for selection
            item.Control.Modulate = selected ? new Color(1, 1, 1) : new Color(0.5f, 0.5f, 0.5f);
        }
    }

    private void UpdateConfirmButton()
    {
        if (ConfirmButton != null)
        {
            bool canConfirm = selectedIdentifiers.Count >= minimumSelections;
            ConfirmButton.Disabled = !canConfirm;
            ConfirmButton.Text = minimumSelections > 0 
                ? $"Confirm ({selectedIdentifiers.Count}/{minimumSelections})"
                : $"Discard {selectedIdentifiers.Count} Card(s)";
        }
    }

    private void HandleConfirmPressed()
    {
        if (multiSelectMode && selectedIdentifiers.Count >= minimumSelections)
        {
            confirmCallback?.Invoke(new List<int>(selectedIdentifiers));
        }
    }

    public SignalAwaiter HideModal()
    {
        if (onKeyClicked != null)
        {
            InputManager.Instance.KeyClicked -= onKeyClicked;
            onKeyClicked = null;
        }
        
        // Reset multi-select state
        multiSelectMode = false;
        minimumSelections = 0;
        selectedIdentifiers.Clear();
        confirmCallback = null;
        skipCallback = null;

        var tween2 = GetTree().CreateTween();
        PropertyTweener propertyTweener2 = tween2.TweenProperty(this, "modulate:a", 0, GameSettings.AnimationDurationSeconds);
        propertyTweener2.Finished += () =>
        {
            tween2.Dispose();
            Visible = false;
            ExitButton.Visible = false;
            if (ConfirmButton != null)
                ConfirmButton.Visible = false;
                
            foreach (PresentationItem presentationItem in PresentationItems)
            {
                presentationItem.ItemClicked -= HandleItemClicked;
                presentationItem.ItemClicked -= HandleItemClickedMultiSelect;
            }
            foreach (Control presentationItemControl in PresentationItemControls)
            {
                if (presentationItemControl.GetParent() != null)
                {
                    presentationItemControl.GetParent().RemoveChild(presentationItemControl);
                }
            }
            PresentationItemControls.Clear();
            PresentationItems.Clear();
            EmitSignal(SignalName.OnHide);
        };
        return ToSignal(this, SignalName.OnHide);
    }

    private void UnsubscribeFromEvents()
    {
        // Unsubscribe from button events
        if (IsInstanceValid(ConfirmButton) && onConfirmButtonPressed != null)
        {
            ConfirmButton.Pressed -= onConfirmButtonPressed;
        }
        
        if (IsInstanceValid(ExitButton) && onExitButtonPressed != null)
        {
            ExitButton.Pressed -= onExitButtonPressed;
        }
        
        // Unsubscribe from InputManager events
        if (InputManager.Instance != null && onKeyClicked != null)
        {
            InputManager.Instance.KeyClicked -= onKeyClicked;
        }
    }
}
