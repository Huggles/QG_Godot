using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
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

    private ModalConfig _activeConfig;
    private List<int> _selectedItems = new();
    private List<int> _orderedItems = new();
    private TaskCompletionSource<ModalResult> _tcs;

    private Tween _activeTween;
    public List<PresentationItem> PresentationItems = new();
    public List<Control> PresentationItemControls = new();
    private InputManager.KeyClickedEventHandler _onKeyClicked;

    [Signal] public delegate void OnShowEventHandler();
    [Signal] public delegate void OnHideEventHandler();
    [Signal] public delegate void ItemSelectedEventHandler(int identifier);

    public override void _Ready()
    {
        if (Multiplayer.GetUniqueId() == GetMultiplayerAuthority())
            Current = this;
        Hide();
        LoadUI();
    }

    public void LoadUI()
    {
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

    private void FadeIn(Action onComplete = null)
    {
        _activeTween?.Kill();
        _activeTween = GetTree().CreateTween();
        _activeTween.TweenProperty(this, "modulate:a", 1, GameSettings.DurationShortSeconds)
            .Finished += () => onComplete?.Invoke();
    }

    private void FadeOut(Action onComplete = null)
    {
        _activeTween?.Kill();
        _activeTween = GetTree().CreateTween();
        _activeTween.TweenProperty(this, "modulate:a", 0, GameSettings.DurationShortSeconds)
            .Finished += () => onComplete?.Invoke();
    }

    // ── Primary entry point ─────────────────────────────────────────────────
    public Task<ModalResult> Show(ModalConfig config)
    {
        _activeConfig = config;
        _selectedItems.Clear();
        _orderedItems.Clear();
        _tcs = new TaskCompletionSource<ModalResult>();

        HandlePresentationItems(config.Items, config.Mode != ModalSelectionMode.Display);
        TitleText.Text = config.Title;
        Visible = true;

        ConfirmButton.Visible = config.ShowApplyButton;
        ConfirmButton.Text = config.ApplyLabel;
        ConfirmButton.Disabled = config.MinSelections > 0;
        ExitButton.Visible = config.ShowCancelButton;
        ExitButton.Text = config.CancelLabel;

        if (config.ShowCancelButton)
            ConnectEscapeKey();

        if (config.AutoDismiss && GameSettings.IsAutoDismissModal)
        {
            FadeIn(async () =>
            {
                EmitSignal(SignalName.OnShow);
                await Task.Delay(GameSettings.DurationLong);
                if (Visible) _ = HideModal();
            });
        }
        else
        {
            if (config.AutoDismiss)
                ShowExitButton();
            FadeIn(() => EmitSignal(SignalName.OnShow));
        }

        return _tcs.Task;
    }

    // ── Legacy wrappers ──────────────────────────────────────────────────────
    public SignalAwaiter ShowModal(List<PresentationItem> presentationItems, string title, bool requireSelection)
    {
        _ = Show(requireSelection
            ? ModalConfig.SelectOne(title, presentationItems)
            : ModalConfig.Display(title, presentationItems));
        return ToSignal(this, requireSelection ? SignalName.ItemSelected : SignalName.OnHide);
    }

    public SignalAwaiter ShowModal(List<PresentationItem> presentationItems, string title, Action exitCallback)
    {
        _ = Show(ModalConfig.SelectOne(title, presentationItems, required: false));
        return ToSignal(this, SignalName.ItemSelected);
    }

    public SignalAwaiter ShowModal(List<PresentationItem> presentationItems, string title)
    {
        _ = Show(ModalConfig.Display(title, presentationItems).WithAutoDismiss());
        return ToSignal(this, SignalName.OnHide);
    }

    public SignalAwaiter ShowModalPersistent(List<PresentationItem> presentationItems, string title)
    {
        _ = Show(ModalConfig.Display(title, presentationItems));
        return ToSignal(this, SignalName.OnShow);
    }

    public void ShowExitButton()
    {
        ExitButton.Visible = true;
    }

    public SignalAwaiter ShowModalMultiSelect(List<PresentationItem> presentationItems, string title, int minimumSelections)
    {
        _ = Show(ModalConfig.SelectMany(title, presentationItems, minimumSelections));
        return ToSignal(this, SignalName.OnHide);
    }

    // ── Item rendering ───────────────────────────────────────────────────────
    private void HandlePresentationItems(List<PresentationItem> presentationItems, bool darkInitially)
    {
        const int maxColumns = 7;
        if (presentationItems == null || presentationItems.Count == 0) return;

        foreach (Control old in PresentationItemControls)
            old.QueueFree();
        PresentationItemControls.Clear();
        GridCardContainer.Columns = Math.Min(presentationItems.Count, maxColumns);
        PresentationItems = presentationItems;
        int rows = (int)Math.Ceiling((double)presentationItems.Count / maxColumns);

        foreach (PresentationItem item in presentationItems)
        {
            Control control = item.InitializeControl();
            GridCardContainer.AddChild(control);
            PresentationItemControls.Add(control);
            item.LoadControl();
            item.ItemClicked += HandleItemClick;

            if (darkInitially)
                control.Modulate = new Color(0.5f, 0.5f, 0.5f);
        }

        Vector2 gridSize = new Vector2(0, PresentationItemControls[0].Size.Y * Mathf.Clamp(rows, 1, 2.1f));
        CardScrollContainer.CustomMinimumSize = gridSize;
        CardScrollContainer.Size = gridSize;
        CardScrollContainer.ScrollVertical = 0;
    }

    // ── Click handling ───────────────────────────────────────────────────────
    private void HandleItemClick(int identifier)
    {
        if (_activeConfig == null) return;

        switch (_activeConfig.Mode)
        {
            case ModalSelectionMode.Display:
                break;

            case ModalSelectionMode.MultiSelect:
                if (_selectedItems.Contains(identifier))
                {
                    _selectedItems.Remove(identifier);
                    UpdateItemVisual(identifier, false);
                }
                else if (_activeConfig.MaxSelections < 0 || _selectedItems.Count < _activeConfig.MaxSelections)
                {
                    _selectedItems.Add(identifier);
                    UpdateItemVisual(identifier, true);
                }
                UpdateConfirmButton();
                // Emit ItemSelected for legacy wrappers that await it
                if (_activeConfig.MaxSelections == 1)
                    EmitSignal(SignalName.ItemSelected, identifier);
                break;

            case ModalSelectionMode.Reorder:
                if (_orderedItems.Contains(identifier))
                    _orderedItems.Remove(identifier);
                else
                    _orderedItems.Add(identifier);
                RefreshOrderBadges();
                UpdateConfirmButton();
                break;
        }
    }

    // ── Visual helpers ───────────────────────────────────────────────────────
    private void UpdateItemVisual(int identifier, bool selected)
    {
        PresentationItem item = PresentationItems.Find(p => p.Identifier == identifier);
        if (item?.Control != null)
            item.Control.Modulate = selected ? new Color(1, 1, 1) : new Color(0.5f, 0.5f, 0.5f);
    }

    private void RefreshOrderBadges()
    {
        foreach (PresentationItem item in PresentationItems)
        {
            int pos = _orderedItems.IndexOf(item.Identifier);
            if (pos >= 0)
            {
                item.UpdateOrderBadge(pos + 1);
                item.Control.Modulate = new Color(1, 1, 1);
            }
            else
            {
                item.UpdateOrderBadge(null);
                item.Control.Modulate = new Color(0.5f, 0.5f, 0.5f);
            }
        }
    }

    private void UpdateConfirmButton()
    {
        if (ConfirmButton == null || _activeConfig == null) return;
        int count = _activeConfig.Mode == ModalSelectionMode.Reorder ? _orderedItems.Count : _selectedItems.Count;
        ConfirmButton.Disabled = count < _activeConfig.MinSelections;
        ConfirmButton.Text = _activeConfig.MinSelections > 0
            ? $"{_activeConfig.ApplyLabel} ({count}/{_activeConfig.MinSelections})"
            : $"{_activeConfig.ApplyLabel} ({count})";
    }

    // ── Button handlers ──────────────────────────────────────────────────────
    private void OnConfirmButtonPressed()
    {
        List<int> result = _activeConfig?.Mode == ModalSelectionMode.Reorder
            ? new List<int>(_orderedItems)
            : new List<int>(_selectedItems);
        _tcs?.TrySetResult(new ModalResult(result));
        _ = HideModal();
    }

    private void OnExitButtonPressed()
    {
        _tcs?.TrySetResult(ModalResult.Cancelled);
        _ = HideModal();
    }

    private void ConnectEscapeKey()
    {
        if (_onKeyClicked != null)
            InputManager.Current.KeyClicked -= _onKeyClicked;
        _onKeyClicked = key =>
        {
            if (key.Keycode == Key.Escape) _ = HideModal();
        };
        InputManager.Current.KeyClicked += _onKeyClicked;
    }

    // ── Hide ─────────────────────────────────────────────────────────────────
    public SignalAwaiter HideModal()
    {
        if (_onKeyClicked != null)
        {
            InputManager.Current.KeyClicked -= _onKeyClicked;
            _onKeyClicked = null;
        }

        // Resolve TCS if still pending (covers external HideModal calls)
        _tcs?.TrySetResult(ModalResult.Cancelled);

        FadeOut(() =>
        {
            Visible = false;
            ExitButton.Visible = false;
            if (ConfirmButton != null)
                ConfirmButton.Visible = false;

            foreach (PresentationItem item in PresentationItems)
                item.ItemClicked -= HandleItemClick;

            foreach (Control control in PresentationItemControls)
            {
                if (control.GetParent() != null)
                    control.GetParent().RemoveChild(control);
            }

            PresentationItemControls.Clear();
            PresentationItems.Clear();

            EmitSignal(SignalName.OnHide,
                new PresentationItemResponse { SelectedItems = new List<int>(_selectedItems) });

            _selectedItems.Clear();
            _orderedItems.Clear();
            _activeConfig = null;
            _tcs = null;
        });

        return ToSignal(this, SignalName.OnHide);
    }

    // ── Nested types ─────────────────────────────────────────────────────────
    public partial class PresentationItemResponse : GodotObject
    {
        public List<int> SelectedItems;
    }
}
