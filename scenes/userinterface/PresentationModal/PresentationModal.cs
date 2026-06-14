using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class PresentationModal : Control, LoadableUI
{
    public static PresentationModal Current;
    public RichTextLabel TitleText => GetNode<RichTextLabel>("%TitleText");
    public PanelContainer PanelContainer => GetNode<PanelContainer>("%PanelContainer");
    public GridContainer GridCardContainer => GetNode<GridContainer>("%GridCardContainer");
    public ScrollContainer CardScrollContainer => GetNode<ScrollContainer>("%CardScrollContainer");    
    public Button ExitButton => GetNode<Button>("%ExitButton");
    public Button ConfirmButton => GetNode<Button>("%ConfirmButton");

    // Multi-select support
    public bool RequireSelection;
    private bool multiSelectMode = false;
    private int minimumSelections = 0;
    private List<int> selectedIdentifiers = new List<int>();
    public List<PresentationItem> PresentationItems = new List<PresentationItem>();
    public List<Control> PresentationItemControls = new List<Control>();

    private InputManager.KeyClickedEventHandler onKeyClicked;

    [Signal] public delegate void OnShowEventHandler();
    [Signal] public delegate void OnHideEventHandler();
    [Signal] public delegate void ItemSelectedEventHandler(int identifier);


    public override void _Ready()
    {
        if(Multiplayer.GetUniqueId() == GetMultiplayerAuthority())
        {
            Current = this;
            
        } 
        // Hide by default until LoadUI is called
        Hide();
        LoadUI();
    }
    public void LoadUI()
    {   
        
        // Try to get ConfirmButton if it exists in the scene
        if (ConfirmButton != null)
        {
            ConfirmButton.Pressed += OnConfirmButtonPressed;
            ConfirmButton.Visible = false;
        }
        Visible = false;
        Modulate = new Color(1, 1, 1, 0);
        ExitButton.Visible = false;
        ExitButton.Pressed += OnExitButtonPressed;
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
            ShowExitButton();
            
            // Unsubscribe first to prevent duplicate connections
            if (onKeyClicked != null)
            {
                InputManager.Current.KeyClicked -= onKeyClicked;
            }
            onKeyClicked = HandleKeyboardInput;
            InputManager.Current.KeyClicked += onKeyClicked;
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
        PropertyTweener propertyTweener1 = tween1.TweenProperty(this, "modulate:a", 1, GameSettings.DurationShortSeconds);
        propertyTweener1.Finished += async () =>
        {
            DebugUtilities.PrintPeerFinest("PresentationModal shown");
            EmitSignal(SignalName.OnShow);
            await Task.Delay(GameSettings.DurationLong);
            tween1.Dispose();

            if (GameSettings.DurationShortSeconds > 0)
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
        PropertyTweener propertyTweener1 = tween1.TweenProperty(this, "modulate:a", 1, GameSettings.DurationShortSeconds);
        
        propertyTweener1.Finished += async () =>
        {   
            tween1.Dispose();
            EmitSignal(SignalName.OnShow);
        };
        return ToSignal(this, SignalName.OnShow);        
    }

    public void ShowExitButton()
    {
        ExitButton.Visible = true;        
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
    public SignalAwaiter ShowModalMultiSelect(List<PresentationItem> presentationItems, string title, int minimumSelections)
    {
        this.multiSelectMode = true;
        this.minimumSelections = minimumSelections;
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
        
        if (minimumSelections == 0)
        {
            ShowExitButton();
        }

        var tween1 = GetTree().CreateTween();
        PropertyTweener propertyTweener1 = tween1.TweenProperty(this, "modulate:a", 1, GameSettings.DurationShortSeconds);
        propertyTweener1.Finished += () =>
        {
            DebugUtilities.PrintPeerFinest("PresentationModal shown");
            EmitSignal(SignalName.OnShow);
            tween1.Dispose();
        };
        return ToSignal(this, SignalName.OnHide);
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
    private void OnConfirmButtonPressed()
    {
        _ = HideModal();
    }

    private void OnExitButtonPressed()
    {
        selectedIdentifiers.Clear();
        _ = HideModal();
    }

    public SignalAwaiter HideModal()
    {
        if (onKeyClicked != null)
        {
            InputManager.Current.KeyClicked -= onKeyClicked;
            onKeyClicked = null;
        }
        
        // Reset multi-select state
        multiSelectMode = false;
        minimumSelections = 0;

        var tween2 = GetTree().CreateTween();
        PropertyTweener propertyTweener2 = tween2.TweenProperty(this, "modulate:a", 0, GameSettings.DurationShortSeconds);
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
            EmitSignal(SignalName.OnHide, new PresentationItemResponse { SelectedItems = new List<int>(selectedIdentifiers) });
            selectedIdentifiers.Clear();
        };
        return ToSignal(this, SignalName.OnHide);
    }

    public partial class PresentationItemResponse : GodotObject
    {
        public List<int> SelectedItems;
       
    }
}
