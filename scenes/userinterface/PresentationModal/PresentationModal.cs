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


    
    public List<PresentationItem> PresentationItems = new List<PresentationItem>();
    public List<Control> PresentationItemControls = new List<Control>();


    [Signal] public delegate void OnShowEventHandler();
    [Signal] public delegate void OnHideEventHandler();
    [Signal] public delegate void ItemSelectedEventHandler(int identifier);


    public override void _Ready()
    {
        Instance = this;
        EventBus.Emit(EventBus.SignalName.UserInterfaceLoaded, "PresentationModal");
    }

    public void LoadUI()
    {
        TitleText = GetNode<RichTextLabel>("%TitleText");
        PanelContainer = GetNode<PanelContainer>("%PanelContainer");
        GridCardContainer = GetNode<GridContainer>("%GridCardContainer");
        ExitButton = GetNode<Button>("%ExitButton");
        CardScrollContainer = GetNode<ScrollContainer>("%CardScrollContainer");

        Visible = false;
        Modulate = new Color(1, 1, 1, 0);
        ExitButton.Visible = false;
    }

    public SignalAwaiter ShowModal(List<PresentationItem> presentationItems, string title)
    {
        return ShowModal(presentationItems, title, false);
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
            InputManager.Instance.KeyClicked += HandleKeyboardInput;
        }        
        ShowModal(presentationItems, title, -1);        
        return ToSignal(this, SignalName.ItemSelected);
    }

    public SignalAwaiter ShowModal(List<PresentationItem> presentationItems, string title, float duration)
    {
        HandlePresentationItems(presentationItems);
        TitleText.Text = title;
        Visible = true;

        var tween1 = GetTree().CreateTween();
        PropertyTweener propertyTweener1 = tween1.TweenProperty(this, "modulate:a", 1, 1);
        propertyTweener1.Finished += async () =>
        {
            EmitSignal(SignalName.OnShow);
            await Task.Delay((int)duration);
            tween1.Dispose();

            if (duration > 0)
            {
                HideModal();
            }
        };
        if (duration > 0)
        {
            return ToSignal(this, SignalName.OnHide);
        }
        else
        {
            return ToSignal(this, SignalName.OnShow);
        }
        
    }

    public SignalAwaiter HideModal()
    {
        InputManager.Instance.KeyClicked -= HandleKeyboardInput;
        var tween2 = GetTree().CreateTween();
        PropertyTweener propertyTweener2 = tween2.TweenProperty(this, "modulate:a", 0, 1);
        propertyTweener2.Finished += () =>
        {
            tween2.Dispose();
            Visible = false;
            ExitButton.Visible = false;
            foreach (PresentationItem presentationItem in PresentationItems)
            {
                presentationItem.ItemClicked -= HandleItemClicked;
            }
            foreach (Control presentationItemControl in PresentationItemControls)
            {
                if (presentationItemControl.GetParent() != null)
                {
                    presentationItemControl.GetParent().RemoveChild(presentationItemControl);
                }
            }
            PresentationItemControls.Clear();
            EmitSignal(SignalName.OnHide);
        };
        return ToSignal(this, SignalName.OnHide);
    }

    public void ShowExitButton(Action callback)
    {
        ExitButton.Visible = true;
        ExitButton.Pressed += () =>
        {
            callback.Invoke();
        };
    }

    private void HandlePresentationItems(List<PresentationItem> presentationItems)
    {
        if (presentationItems != null && presentationItems.Count > 0)
        {
            foreach (PresentationItem presentationItem in presentationItems)
            {
                Control control = presentationItem.InitializeControl();
                GridCardContainer.AddChild(control);

                PresentationItemControls.Add(control);
                presentationItem.LoadControl();
                presentationItem.ItemClicked += HandleItemClicked;
            }
            Vector2 gridSize = new Vector2(0, presentationItems.Count > GridCardContainer.Columns ? PresentationItemControls[0].Size.Y * 2 : PresentationItemControls[0].Size.Y);
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
}
