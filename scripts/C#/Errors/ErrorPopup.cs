using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// In-game error overlay, built entirely in C# (same approach as DebugOverlay.BuildUI) so it needs
/// no .tscn and no edit to user_interface.tscn.
///
/// Deliberately NOT the game's modal stack: that lives inside the per-player user_interface.tscn which
/// only exists once a game has loaded, it is built for card grids rather than text, and
/// ModalStack.Current is itself a live null-deref source. This overlay has to work in the
/// menu, during loading, and on a client whose UI failed to build — so it is owned by the
/// ErrorReporter autoload and therefore survives ChangeSceneToFile.
/// </summary>
public partial class ErrorPopup : CanvasLayer
{
    // The gameplay UI CanvasLayer sits at the default layer 1 and orders its children with z_index
    // (ModalStack 100, DebugOverlay 99). z_index never crosses CanvasLayers, so any
    // layer >= 2 wins; 128 is the top of the editor's conventional range.
    private const int OverlayLayer = 128;

    private GameError _current;
    private int _index;

    /// <summary>
    /// True when the popup was opened by the history hotkey rather than raised by a failure. Only
    /// affects presentation — whether Continue is offered is decided by
    /// <c>ErrorReporter.HasPendingStall</c>, so opening history while the loop IS stalled still lets
    /// you resume, and opening it when nothing is wrong cannot skip a turn step.
    /// </summary>
    private bool _historyMode;

    private Label _titleLabel;
    private Label _originLabel;
    private Label _contextLabel;
    private RichTextLabel _messageLabel;
    private Button _traceToggle;
    private ScrollContainer _traceScroll;
    private TextEdit _traceText;
    private Label _counterLabel;
    private Button _prevButton;
    private Button _nextButton;
    private Button _continueButton;
    private Button _quitButton;
    private Button _retryButton;

    private bool _inputWasEnabled;

    public override void _Ready()
    {
        Layer = OverlayLayer;
        // Stay interactive even if someone later introduces tree pausing. Note we deliberately do
        // NOT pause the tree ourselves: pausing halts tweens and SceneTreeTimer while Task.Delay
        // keeps running, which would hang every `await ToSignal(tween, Finished)` in the animation
        // classes — manufacturing the exact deadlock this whole system exists to remove.
        ProcessMode = ProcessModeEnum.Always;
        BuildUI();
        Hide();
    }

    // ── Construction ─────────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Backdrop: full-rect and MouseFilter.Stop, so clicks cannot reach the board. Board clicks
        // go through Area picking and InputManager uses _UnhandledInput (which runs after GUI), so
        // this covers the mouse. Keyboard is handled separately in SetGameInputEnabled.
        Control root = new Control
        {
            Name = "ErrorOverlayRoot",
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        ColorRect dim = new ColorRect { Color = new Color(0, 0, 0, 0.72f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        dim.MouseFilter = Control.MouseFilterEnum.Stop;
        root.AddChild(dim);

        CenterContainer centre = new CenterContainer();
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        centre.MouseFilter = Control.MouseFilterEnum.Pass;
        root.AddChild(centre);

        PanelContainer panel = new PanelContainer { CustomMinimumSize = new Vector2(900, 0) };
        centre.AddChild(panel);

        MarginContainer margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        panel.AddChild(margin);

        VBoxContainer box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        margin.AddChild(box);

        // ── Header ───────────────────────────────────────────────────────────
        HBoxContainer headerRow = new HBoxContainer();
        box.AddChild(headerRow);

        _titleLabel = new Label
        {
            Text = "An error occurred",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _titleLabel.AddThemeColorOverride("font_color", new Color(1f, 0.42f, 0.38f));
        _titleLabel.AddThemeFontSizeOverride("font_size", 26);
        headerRow.AddChild(_titleLabel);

        _counterLabel = new Label { Text = "" };
        _counterLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.75f));
        headerRow.AddChild(_counterLabel);

        _originLabel = new Label { Text = "" };
        _originLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.5f));
        box.AddChild(_originLabel);

        _contextLabel = new Label
        {
            Text = "",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _contextLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.78f, 0.86f));
        box.AddChild(_contextLabel);

        box.AddChild(new HSeparator());

        // ── Message ──────────────────────────────────────────────────────────
        // BbcodeEnabled stays false: exception text is arbitrary and full of square brackets.
        _messageLabel = new RichTextLabel
        {
            BbcodeEnabled = false,
            SelectionEnabled = true,
            FitContent = true,
            CustomMinimumSize = new Vector2(0, 60),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        box.AddChild(_messageLabel);

        // ── Stack trace (collapsed by default) ───────────────────────────────
        _traceToggle = new Button
        {
            Text = "▸ Stack trace",
            ToggleMode = true,
            Alignment = HorizontalAlignment.Left,
            Flat = true
        };
        _traceToggle.Toggled += OnTraceToggled;
        box.AddChild(_traceToggle);

        _traceScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 320),
            Visible = false
        };
        box.AddChild(_traceScroll);

        // A read-only TextEdit gives selection and native Ctrl+C for free, and does not parse markup.
        _traceText = new TextEdit
        {
            Editable = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            ScrollFitContentHeight = true
        };
        _traceScroll.AddChild(_traceText);

        box.AddChild(new HSeparator());

        // ── Buttons ──────────────────────────────────────────────────────────
        HBoxContainer buttonRow = new HBoxContainer();
        buttonRow.AddThemeConstantOverride("separation", 10);
        box.AddChild(buttonRow);

        Button copyButton = new Button { Text = "Copy" };
        copyButton.Pressed += OnCopyPressed;
        buttonRow.AddChild(copyButton);

        _prevButton = new Button { Text = "◂ Prev" };
        _prevButton.Pressed += () => Navigate(-1);
        buttonRow.AddChild(_prevButton);

        _nextButton = new Button { Text = "Next ▸" };
        _nextButton.Pressed += () => Navigate(1);
        buttonRow.AddChild(_nextButton);

        buttonRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        _quitButton = new Button { Text = "Quit to menu" };
        _quitButton.Pressed += OnQuitPressed;
        buttonRow.AddChild(_quitButton);

        _continueButton = new Button { Text = "Continue" };
        _continueButton.Pressed += OnContinuePressed;
        buttonRow.AddChild(_continueButton);

        // Last, so it sits rightmost where the primary action goes. Only ever visible for a timed-out
        // input request, where the request itself is still holding the turn loop and can therefore be
        // asked again — an ordinary failure has already unwound its step, leaving nothing to re-send.
        _retryButton = new Button { Text = "Retry" };
        _retryButton.Pressed += OnRetryPressed;
        buttonRow.AddChild(_retryButton);
    }

    // ── Presentation ─────────────────────────────────────────────────────────

    /// <summary>Show <paramref name="error"/>, or bring the overlay up if it is not visible yet.</summary>
    public void Present(GameError error)
    {
        if (error == null) return;

        _historyMode = false;
        _index = Math.Max((ErrorReporter.Instance?.History.Count ?? 1) - 1, 0);
        _current = error;

        Render();

        if (!Visible)
        {
            Show();
            SetGameInputEnabled(false);
        }
    }

    /// <summary>
    /// Open on the newest recorded error and let the player browse back through the history. Entry
    /// point for the <c>debug_error_history</c> hotkey.
    /// </summary>
    public void PresentHistory()
    {
        _historyMode = true;

        IReadOnlyList<GameError> history = ErrorReporter.Instance?.History;
        _index = Math.Max((history?.Count ?? 0) - 1, 0);
        _current = history != null && history.Count > 0 ? history[_index] : null;

        Render();

        if (!Visible)
        {
            Show();
            SetGameInputEnabled(false);
        }
    }

    /// <summary>Close from the hotkey — same teardown as the Close button, without resuming anything.</summary>
    public void CloseFromHotkey() => Dismiss();

    /// <summary>Re-render the current error — used when an occurrence count changes.</summary>
    public void Refresh()
    {
        if (Visible) Render();
    }

    private void Render()
    {
        IReadOnlyList<GameError> history = ErrorReporter.Instance?.History;
        int total = history?.Count ?? 0;

        if (total == 0 || _current == null)
        {
            RenderEmpty();
            return;
        }

        // Ring eviction can shift indices under an open popup, so re-resolve by identity rather than
        // trusting the stored index; fall back to the newest if this entry has aged out.
        int found = IndexOf(history, _current);
        if (found < 0)
        {
            _index = total - 1;
            _current = history[_index];
        }
        else
        {
            _index = found;
        }

        bool isHost = IsHost();
        bool stalled = ErrorReporter.Instance?.HasPendingStall ?? false;
        bool canResume = ErrorReporter.Instance?.PendingSeverity != ErrorSeverity.Unrecoverable;

        // Not gated on _historyMode, matching how `stalled` is not: opening the history by hotkey while a
        // decision is genuinely outstanding must still let you make it.
        bool awaitingInput = (ErrorReporter.Instance?.HasPendingInputDecision ?? false) && isHost;

        _titleLabel.Text = _historyMode
            ? $"Error history ({total})"
            : _current.Severity switch
            {
                ErrorSeverity.Soft => "An error occurred (recovered)",
                ErrorSeverity.Unrecoverable => "An unrecoverable error occurred",
                _ => "An error occurred"
            };

        string occurrences = _current.Occurrences > 1 ? $"  ×{_current.Occurrences}" : "";
        _counterLabel.Text = $"{_index + 1}/{total}{occurrences}";

        // Make it obvious why a recorded error was never seen.
        string recoveredNote = _current.Severity == ErrorSeverity.Soft
            ? "RECOVERED · no popup was shown\n"
            : "";
        _originLabel.Text = recoveredNote + _current.OriginSummary;

        _contextLabel.Text = _current.Context;
        _messageLabel.Text = $"{_current.ExceptionType}\n{_current.Message}";
        _traceText.Text = _current.StackTrace ?? "";
        _traceToggle.Visible = true;   // RenderEmpty hides it

        // Always visible while browsing, so it is clear the list can be paged even at one entry.
        _prevButton.Visible = _historyMode || total > 1;
        _nextButton.Visible = _historyMode || total > 1;
        _prevButton.Disabled = _index <= 0;
        _nextButton.Disabled = _index >= total - 1;

        _quitButton.Visible = stalled || awaitingInput;
        _retryButton.Visible = awaitingInput;

        if (awaitingInput)
        {
            // Checked before `stalled` on purpose. The loop is being held by a live await inside
            // SendInputRequest, so the actionable choice is about that request; Continue must resolve it
            // rather than call RequestResume, which would advance the very step still being held.
            _continueButton.Text = "Skip input";
            _continueButton.Disabled = false;
            _continueButton.TooltipText =
                "Gives up on the input and continues the step as if the player had passed. " +
                "A required choice (e.g. discarding down to the hand limit) will not happen at all.";
            string who = _current?.TargetFaction.HasValue == true
                ? _current.TargetFaction.Value.ToString()
                : "the player";
            _retryButton.TooltipText =
                $"Asks {who} for the same input again, with a fresh timer. Nothing has been skipped yet.";
        }
        else if (!stalled)
        {
            // Nothing is waiting on the player — this is a browse, not a decision. Offering Continue
            // here would advance the turn loop past a step that never failed.
            _continueButton.Text = "Close";
            _continueButton.Disabled = false;
            _continueButton.TooltipText = "Closes this window. Nothing is waiting — the game is not paused.";
        }
        else if (!isHost)
        {
            _continueButton.Text = "Dismiss";
            _continueButton.Disabled = false;
            _continueButton.TooltipText = "Only the host can resume the turn loop.";
        }
        else if (!canResume)
        {
            // Mid-mutation before the change reached clients: peers are already divergent and the
            // resync repair path in NetworkApi.RequestResync is commented out, so there is nothing
            // honest to continue into.
            _continueButton.Text = "Continue";
            _continueButton.Disabled = true;
            _continueButton.TooltipText =
                "Unavailable: the game state was modified before clients were notified, so peers have diverged.";
        }
        else
        {
            _continueButton.Text = "Continue";
            _continueButton.Disabled = false;
            _continueButton.TooltipText =
                "Skips the rest of the failed step and resumes play. The game state may be inconsistent.";
        }

        // Retry is the default for a timed-out input: it is the only choice that loses nothing, and Enter
        // must not be the key that silently skips a mandatory discard.
        if (awaitingInput) _retryButton.GrabFocus();
        else _continueButton.GrabFocus();
    }

    /// <summary>
    /// Shown when the hotkey is pressed with nothing recorded. Without this the popup would render a
    /// stale or blank panel and look broken.
    /// </summary>
    private void RenderEmpty()
    {
        _titleLabel.Text = "Error history";
        _counterLabel.Text = "";
        _originLabel.Text = "";
        _contextLabel.Text = "";
        _messageLabel.Text = "No errors recorded this session.";
        _traceText.Text = "";

        _traceToggle.Visible = false;
        _traceScroll.Visible = false;
        _prevButton.Visible = false;
        _nextButton.Visible = false;
        _quitButton.Visible = false;
        _retryButton.Visible = false;

        _continueButton.Text = "Close";
        _continueButton.Disabled = false;
        _continueButton.TooltipText = "";
        _continueButton.GrabFocus();
    }

    private static int IndexOf(IReadOnlyList<GameError> list, GameError target)
    {
        for (int i = 0; i < list.Count; i++)
            if (ReferenceEquals(list[i], target)) return i;
        return -1;
    }

    private void Navigate(int delta)
    {
        IReadOnlyList<GameError> history = ErrorReporter.Instance?.History;
        if (history == null || history.Count == 0) return;

        _index = Math.Clamp(_index + delta, 0, history.Count - 1);
        _current = history[_index];
        Render();
    }

    private void OnTraceToggled(bool pressed)
    {
        _traceScroll.Visible = pressed;
        _traceToggle.Text = pressed ? "▾ Stack trace" : "▸ Stack trace";
    }

    private void OnCopyPressed()
    {
        if (_current == null) return;
        DisplayServer.ClipboardSet(_current.ToPlainText());
    }

    /// <summary>
    /// Re-send the timed-out input request. The awaiting step never unwound, so this resumes exactly
    /// where it was rather than replaying anything.
    /// </summary>
    private void OnRetryPressed()
    {
        Dismiss();
        ErrorReporter.ResolveInputDecision(retry: true);
    }

    private void OnContinuePressed()
    {
        // Read both states BEFORE dismissing — Dismiss acknowledges, which clears the stall.
        bool wasStalled = ErrorReporter.Instance?.HasPendingStall ?? false;
        bool awaitingInput = ErrorReporter.Instance?.HasPendingInputDecision ?? false;

        Dismiss();

        if (!IsHost()) return;

        // "Skip input": the step is still live inside SendInputRequest, so resolving its decision is the
        // whole job. Calling RequestResume as well would advance that same step a second time.
        if (awaitingInput)
        {
            ErrorReporter.ResolveInputDecision(retry: false);
            return;
        }

        if (wasStalled)
            ErrorReporter.RequestResume();
    }

    private void OnQuitPressed()
    {
        Dismiss();
        ErrorReporter.Instance?.AbandonPendingStall();   // leaving the game; resuming is moot
        ErrorReporter.IsShuttingDown = true;

        // Leave any multiplayer session cleanly so a fresh game can be hosted/joined — same
        // teardown as VictoryScreen.OnMainMenuPressed. Deferred because the click arrives during
        // signal processing.
        if (Multiplayer.MultiplayerPeer != null)
            Multiplayer.MultiplayerPeer = null;
        // This path deliberately bypasses SceneFlow, so the Steam lobby release SceneFlow does for
        // leaveSession has to be repeated here. No-op unless the session was Steam-hosted.
        SteamworksApi.Instance?.LeaveCurrentLobby();
        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://scenes/menu/MainMenu.tscn");
    }

    private void Dismiss()
    {
        Hide();
        SetGameInputEnabled(true);

        // Acknowledge, do not clear: the errors stay in the history so the hotkey can bring them back.
        ErrorReporter.Instance?.AcknowledgeAll();

        _current = null;
        _index = 0;
        _historyMode = false;
    }

    // ── Input gating ─────────────────────────────────────────────────────────

    /// <summary>
    /// The MouseFilter.Stop backdrop covers mouse input, but keyboard still reaches
    /// InputManager._UnhandledInput → KeyClicked, which would let Escape cancel an underlying modal
    /// while this popup is up, and camera panning keeps running from _Process. Toggle both off.
    /// </summary>
    private void SetGameInputEnabled(bool enabled)
    {
        try
        {
            InputManager manager = InputManager.Current;
            if (manager == null || !IsInstanceValid(manager)) return;

            if (enabled)
            {
                manager.SetProcessUnhandledInput(_inputWasEnabled);
                manager.SetProcess(_inputWasEnabled);
            }
            else
            {
                _inputWasEnabled = manager.IsProcessingUnhandledInput();
                manager.SetProcessUnhandledInput(false);
                manager.SetProcess(false);
            }
        }
        catch
        {
            // Never let input gating be the thing that fails while reporting an error.
        }
    }

    private bool IsHost()
    {
        try
        {
            if (Multiplayer?.MultiplayerPeer == null) return true;   // single process, no session
            return Multiplayer.IsServer();
        }
        catch { return false; }
    }
}
