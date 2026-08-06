using Godot;
using System;

/// <summary>
/// In-game error overlay, built entirely in C# (same approach as DebugOverlay.BuildUI) so it needs
/// no .tscn and no edit to user_interface.tscn.
///
/// Deliberately NOT PresentationModal: that lives inside the per-player user_interface.tscn which
/// only exists once a game has loaded, it is built for card grids rather than text, and
/// PresentationModal.Current is itself a live null-deref source. This overlay has to work in the
/// menu, during loading, and on a client whose UI failed to build — so it is owned by the
/// ErrorReporter autoload and therefore survives ChangeSceneToFile.
/// </summary>
public partial class ErrorPopup : CanvasLayer
{
    // The gameplay UI CanvasLayer sits at the default layer 1 and orders its children with z_index
    // (PresentationModal 100, DebugOverlay 99). z_index never crosses CanvasLayers, so any
    // layer >= 2 wins; 128 is the top of the editor's conventional range.
    private const int OverlayLayer = 128;

    private GameError _current;
    private int _index;

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
    }

    // ── Presentation ─────────────────────────────────────────────────────────

    /// <summary>Show <paramref name="error"/>, or bring the overlay up if it is not visible yet.</summary>
    public void Present(GameError error)
    {
        if (error == null) return;

        int queueIndex = ErrorReporter.Instance?.Queue.Count - 1 ?? 0;
        _index = Math.Max(queueIndex, 0);
        _current = error;

        Render();

        if (!Visible)
        {
            Show();
            SetGameInputEnabled(false);
        }
    }

    /// <summary>Re-render the current error — used when an occurrence count changes.</summary>
    public void Refresh()
    {
        if (Visible) Render();
    }

    private void Render()
    {
        if (_current == null) return;

        bool isHost = IsHost();
        bool recoverable = _current.Severity != ErrorSeverity.Unrecoverable;

        _titleLabel.Text = _current.Severity switch
        {
            ErrorSeverity.Soft => "An error occurred (recovered)",
            ErrorSeverity.Unrecoverable => "An unrecoverable error occurred",
            _ => "An error occurred"
        };

        string occurrences = _current.Occurrences > 1 ? $"  ×{_current.Occurrences}" : "";
        int total = ErrorReporter.Instance?.Queue.Count ?? 1;
        _counterLabel.Text = total > 1 ? $"{_index + 1}/{total}{occurrences}" : occurrences.TrimStart();

        _originLabel.Text = _current.OriginSummary;
        _contextLabel.Text = _current.Context;
        _messageLabel.Text = $"{_current.ExceptionType}\n{_current.Message}";
        _traceText.Text = _current.StackTrace ?? "";

        _prevButton.Disabled = _index <= 0;
        _nextButton.Disabled = _index >= total - 1;
        _prevButton.Visible = total > 1;
        _nextButton.Visible = total > 1;

        // Only the server drives the turn loop, so only the host can actually resume it.
        if (!isHost)
        {
            _continueButton.Text = "Dismiss";
            _continueButton.Disabled = false;
            _continueButton.TooltipText = "Only the host can resume the turn loop.";
        }
        else if (_current.Severity == ErrorSeverity.Soft)
        {
            // Already recovered in place — the loop never stopped, so there is nothing to resume and
            // advancing would silently skip a turn step.
            _continueButton.Text = "Dismiss";
            _continueButton.Disabled = false;
            _continueButton.TooltipText = "This step was abandoned but play continued — nothing to resume.";
        }
        else if (!recoverable)
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

        _continueButton.GrabFocus();
    }

    private void Navigate(int delta)
    {
        var queue = ErrorReporter.Instance?.Queue;
        if (queue == null || queue.Count == 0) return;

        _index = Math.Clamp(_index + delta, 0, queue.Count - 1);
        _current = queue[_index];
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

    private void OnContinuePressed()
    {
        Dismiss();
        if (IsHost())
            ErrorReporter.RequestResume();
    }

    private void OnQuitPressed()
    {
        Dismiss();
        ErrorReporter.IsShuttingDown = true;

        // Leave any multiplayer session cleanly so a fresh game can be hosted/joined — same
        // teardown as VictoryScreen.OnMainMenuPressed. Deferred because the click arrives during
        // signal processing.
        if (Multiplayer.MultiplayerPeer != null)
            Multiplayer.MultiplayerPeer = null;
        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://scenes/menu/MainMenu.tscn");
    }

    private void Dismiss()
    {
        Hide();
        SetGameInputEnabled(true);
        ErrorReporter.Instance?.ClearQueue();
        _current = null;
        _index = 0;
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
