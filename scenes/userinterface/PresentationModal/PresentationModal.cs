using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// One modal, for one <see cref="ModalConfig"/>, resolved once and then freed. The scene root is the
/// styled panel itself and shrinks to its content, so <see cref="ModalStack"/> can place several of
/// them side by side; all the full-screen chrome — backdrop, click-blocking, gutter, centring —
/// belongs to that host, once.
///
/// One-shot is what makes a modal prompt recallable. The old single instance answered its pending
/// request as <see cref="ModalResult.Cancelled"/> every time anything else needed the modal, so
/// dismissing was answering and there was nothing to come back to. Here putting a prompt aside is
/// <see cref="Park"/> — <c>Visible = false</c> on an intact subtree — which leaves the
/// <see cref="TaskCompletionSource{TResult}"/>, the selection lists and the item subscriptions
/// completely untouched.
/// </summary>
public partial class PresentationModal : PanelContainer, LoadableUI
{
    public RichTextLabel TitleText => GetNode<RichTextLabel>("%TitleText");
    public GridContainer GridCardContainer => GetNode<GridContainer>("%GridCardContainer");
    public ScrollContainer CardScrollContainer => GetNode<ScrollContainer>("%CardScrollContainer");
    public Button ExitButton => GetNode<Button>("%ExitButton");
    public Button ConfirmButton => GetNode<Button>("%ConfirmButton");
    public Button ParkButton => GetNodeOrNull<Button>("%ParkButton");

    /// <summary>The stack that owns this instance. Set by <see cref="ModalStack.Open"/> before Prepare.</summary>
    internal ModalStack Host;

    private ModalConfig _activeConfig;
    private List<int> _selectedItems = new();
    private List<int> _orderedItems = new();
    private TaskCompletionSource<ModalResult> _tcs;

    private Tween _activeTween;
    public List<PresentationItem> PresentationItems = new();
    public List<Control> PresentationItemControls = new();

    /// <summary>Resolved (answered, cancelled or abandoned). Nothing may reopen or re-place it.</summary>
    private bool _closed;

    /// <summary>On screen, as decided by the host's layout pass. Not the same as parked.</summary>
    private bool _placed;

    /// <summary>The player put this aside deliberately; only a recall brings it back.</summary>
    public bool IsParked { get; private set; }

    /// <summary>Asks the player for something, as opposed to only showing them something.</summary>
    public bool IsRequest => _activeConfig != null && _activeConfig.Mode != ModalSelectionMode.Display;

    public bool CanPark => IsRequest && !_activeConfig.AutoDismiss && !IsParked && !_closed
                           && _tcs != null && !_tcs.Task.IsCompleted;

    public Task<ModalResult> Result => _tcs.Task;

    public string Title => _activeConfig?.Title ?? "(untitled)";

    /// <summary>This modal's identity for <see cref="ModalStack"/>'s toggle check, or null.</summary>
    public string DedupeKey => _activeConfig?.DedupeKey;

    public override void _Ready()
    {
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
        if (ParkButton != null)
        {
            ParkButton.Pressed += OnParkButtonPressed;
            ParkButton.Visible = false;
            ParkButton.TooltipText = "Put this aside — bring it back with the button in the bottom-left menu.";
        }
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

    // ── Setup ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Build this instance's content. Called once, by <see cref="ModalStack.Open"/>, after the node is
    /// in the tree so the %-unique lookups resolve and the grid can be measured.
    ///
    /// Deliberately does not show anything: placement is the host's decision, and Escape is the host's
    /// to route, because with several modals up there is no single "top" for either.
    /// </summary>
    public Task<ModalResult> Prepare(ModalConfig config)
    {
        if (_activeConfig != null)
        {
            DebugUtilities.PrintPeerErrorRaw(
                "PresentationModal.Prepare called twice on one instance; ignoring the second config.");
            return _tcs.Task;
        }

        _activeConfig = config;
        _selectedItems.Clear();
        _orderedItems.Clear();
        _tcs = new TaskCompletionSource<ModalResult>();

        HandlePresentationItems(config.Items, config.Mode != ModalSelectionMode.Display);
        TitleText.Text = config.Title;

        ConfirmButton.Visible = config.ShowApplyButton;
        ConfirmButton.Text = config.ApplyLabel;
        ConfirmButton.Disabled = config.MinSelections > 0;
        ExitButton.Visible = config.ShowCancelButton;
        ExitButton.Text = config.CancelLabel;
        if (ParkButton != null)
            ParkButton.Visible = config.ShowParkButton;

        if (config.AutoDismiss && GameSettings.IsAutoDismissModal)
        {
            // Not gated on Visible any more. That guard let a stale timer from an already-dismissed
            // modal hide the *next* one, and it would now be worse: an overflow-hidden auto-dismiss
            // modal would never resolve at all, stalling whichever animation is awaiting it.
            _ = DismissAfterDelay();
        }
        else if (config.AutoDismiss)
        {
            ShowExitButton();
        }

        return _tcs.Task;
    }

    private async Task DismissAfterDelay()
    {
        await Task.Delay(GameSettings.DurationLong);
        if (!_closed) Close(ModalResult.Cancelled);
    }

    private void ShowExitButton()
    {
        ExitButton.Visible = true;
    }

    // ── Placement, parking and recall ────────────────────────────────────────

    /// <summary>
    /// The host's only visibility lever. Un-placing is instant rather than faded so a relayout settles
    /// in a single pass; the player's own <see cref="Park"/> is the one that fades.
    /// </summary>
    internal void SetPlaced(bool placed)
    {
        if (_closed || placed == _placed) return;
        _placed = placed;

        if (placed)
        {
            Visible = true;
            FadeIn();
        }
        else
        {
            _activeTween?.Kill();
            Visible = false;
            Modulate = new Color(1, 1, 1, 0);
        }
    }

    /// <summary>
    /// Put this prompt aside without answering it. Nothing about <c>_tcs</c>, the selection lists, the
    /// reorder badges, the scroll position or the item subscriptions is touched, so a recall returns the
    /// player to exactly where they were. A hidden Control receives no mouse input, which is why the
    /// item subscriptions can safely stay live.
    /// </summary>
    public bool Park()
    {
        if (!CanPark) return false;

        IsParked = true;
        // Cleared here rather than in the fade callback so the host's relayout sees it already unplaced
        // and its SetPlaced(false) becomes a no-op, letting this fade play out.
        _placed = false;
        FadeOut(() =>
        {
            // A recall during the fade wins: it has already put this back on screen.
            if (!IsParked) return;
            Visible = false;
            // The host measures "is anything on screen" off its row's visible children, so it cannot know
            // this fade finished. Without telling it, the full-screen backdrop would stay up over a
            // prompt nobody can see — with parking, that would block the very board the player parked to
            // look at.
            Host?.OnModalVisibilityChanged();
        });
        Host?.OnModalParked(this);
        return true;
    }

    /// <summary>
    /// Mark this no longer put aside; the host's relayout is what actually places it again. Emits
    /// nothing — a recall is not an answer.
    /// </summary>
    internal void Unpark() => IsParked = false;

    /// <summary>
    /// Escape, routed here by the host because only it knows which of several modals is on top.
    /// Returns whether the key was consumed.
    /// </summary>
    public bool HandleEscape()
    {
        if (_closed || _activeConfig == null) return false;

        // A modal with a Cancel button: Escape cancels, exactly as before.
        if (_activeConfig.ShowCancelButton)
        {
            OnExitButtonPressed();
            return true;
        }
        // A mandatory prompt has no way out at all, so Escape reads as "get this out of my way"
        // rather than as an answer.
        if (CanPark)
        {
            Park();
            return true;
        }
        return false;
    }

    // ── Item rendering ───────────────────────────────────────────────────────
    private void HandlePresentationItems(List<PresentationItem> presentationItems, bool darkInitially)
    {
        const int maxColumns = 7;
        if (presentationItems == null || presentationItems.Count == 0) return;

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

        CardScrollContainer.CustomMinimumSize =
            new Vector2(0, PresentationItemControls[0].Size.Y * Mathf.Clamp(rows, 1, 2.1f));
        CardScrollContainer.ScrollVertical = 0;
    }

    // ── Click handling ───────────────────────────────────────────────────────
    private void HandleItemClick(int identifier)
    {
        if (_activeConfig == null || _closed) return;

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
        Close(new ModalResult(result));
    }

    private void OnExitButtonPressed() => Close(ModalResult.Cancelled);

    private void OnParkButtonPressed() => Park();

    // ── Resolution ───────────────────────────────────────────────────────────

    /// <summary>
    /// Close this modal as though the player dismissed it, releasing whatever awaits it. Used when
    /// re-opening the same modal toggles it shut, by the host's <see cref="ModalStack.CancelAll"/>, and
    /// so by error recovery.
    ///
    /// Every caller awaits the task from <see cref="Prepare"/>, so completing <c>_tcs</c> is sufficient —
    /// the signal emits this once needed went away with the legacy wrappers. Correct for a parked
    /// instance too: the guard this replaced tested <c>Visible</c> and so would have skipped one.
    /// </summary>
    public void Dismiss()
    {
        if (_closed) return;
        Close(ModalResult.Cancelled);
    }

    private void Close(ModalResult result)
    {
        if (_closed) return;
        _closed = true;

        // No need to stop input explicitly: HandleItemClick and every button handler bail on _closed.

        // Out of the stack *before* resolving: the handler this releases may open the next modal, and it
        // must not see an instance that has already answered still competing for space. The node itself
        // lives on through the fade, so the row does not reflow mid-animation.
        Host?.OnModalResolved(this);

        _tcs?.TrySetResult(result);

        // Freed on the way out rather than at once, so an answered modal still fades like it always did.
        if (IsInsideTree()) FadeOut(QueueFree);
        else QueueFree();
    }

    /// <summary>
    /// <see cref="PresentationItem"/> extends <c>GodotObject</c> rather than <c>Node</c>, so freeing
    /// this node does not free them and they leaked. Done at predelete rather than in
    /// <see cref="Close"/> so the items outlive a click queued in the same frame.
    ///
    /// Safe to own them: <see cref="HandlePresentationItems"/> takes the caller's list, and every
    /// producer (<c>PresentationItemCard.FromCardIds</c>, <c>PresentationItem.ForFactions</c>,
    /// <c>PresentationItemBulletin.Single</c>) allocates fresh items per call.
    /// </summary>
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;

        foreach (PresentationItem item in PresentationItems)
        {
            if (IsInstanceValid(item)) item.Free();
        }
        PresentationItems.Clear();
        PresentationItemControls.Clear();
    }
}
