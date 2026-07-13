using Godot;
using System.Linq;

/// <summary>
/// Top-right HUD panel showing live GameFlow and CardPlayRound debug state.
/// Refreshes on relevant EventBus signals; does not poll each frame.
/// </summary>
public partial class DebugOverlay : PanelContainer
{
    // ── Labels: GameFlow ──────────────────────────────────────────────────────
    private Label _turnLabel;
    private Label _roundLabel;
    private Label _turnStepLabel;
    private Label _factionLabel;
    private Label _stepCounterLabel;

    // ── Toggle button ─────────────────────────────────────────────────────────
    private Button _detailsToggle;

    // ── Labels: CardPlayRound ─────────────────────────────────────────────────
    private Label _cprHeaderLabel;
    private Label _cardPoolLabel;
    private Label _changeEventsLabel;
    private Label _lastChangeEventLabel;
    private Label _reactionTriggerLabel;
    private Label _reactionDepthLabel;

    // ── EventBus handler delegates ────────────────────────────────────────────
    private EventBus.NextStepStartedEventHandler _onNextStepStarted;
    private EventBus.NewTurnStartedEventHandler _onNewTurnStarted;
    private EventBus.CardPlayStartedEventHandler _onCardPlayStarted;
    private EventBus.CardPlayPoolFinishedEventHandler _onCardPlayPoolFinished;
    private EventBus.GameChangeEventAfterEventHandler _onGameChangeEventAfter;

    public override void _Ready()
    {
        BuildUI();
        SubscribeToEvents();
        UpdateDisplay();
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance == null) return;

        if (_onNextStepStarted != null)       EventBus.Instance.NextStepStarted       -= _onNextStepStarted;
        if (_onNewTurnStarted != null)         EventBus.Instance.NewTurnStarted         -= _onNewTurnStarted;
        if (_onCardPlayStarted != null)        EventBus.Instance.CardPlayStarted        -= _onCardPlayStarted;
        if (_onCardPlayPoolFinished != null)   EventBus.Instance.CardPlayPoolFinished   -= _onCardPlayPoolFinished;
        if (_onGameChangeEventAfter != null)   EventBus.Instance.GameChangeEventAfter   -= _onGameChangeEventAfter;
    }

    // ── UI construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Semi-transparent dark panel
        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.05f, 0.05f, 0.05f, 0.82f);
        style.BorderWidthLeft   = 1;
        style.BorderWidthTop    = 1;
        style.BorderWidthRight  = 1;
        style.BorderWidthBottom = 1;
        style.BorderColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        style.CornerRadiusTopLeft     = 6;
        style.CornerRadiusTopRight    = 6;
        style.CornerRadiusBottomLeft  = 6;
        style.CornerRadiusBottomRight = 6;
        AddThemeStyleboxOverride("panel", style);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",   8);
        margin.AddThemeConstantOverride("margin_right",  8);
        margin.AddThemeConstantOverride("margin_top",    6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);
        margin.AddChild(vbox);

        // ── GameFlow section ──────────────────────────────────────────────────
        vbox.AddChild(MakeSectionHeader("── GameFlow ──"));

        _turnLabel         = MakeValueLabel(); vbox.AddChild(_turnLabel);
        _roundLabel        = MakeValueLabel(); vbox.AddChild(_roundLabel);
        _turnStepLabel     = MakeValueLabel(); vbox.AddChild(_turnStepLabel);
        _factionLabel      = MakeValueLabel(); vbox.AddChild(_factionLabel);
        _stepCounterLabel  = MakeValueLabel(); vbox.AddChild(_stepCounterLabel);

        // ── Separator ─────────────────────────────────────────────────────────
        var sep = new HSeparator();
        sep.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(sep);

        // ── CardPlayRound section ─────────────────────────────────────────────
        _cprHeaderLabel = MakeSectionHeader("── CardPlayRound ──");
        vbox.AddChild(_cprHeaderLabel);

        _cardPoolLabel        = MakeValueLabel(); vbox.AddChild(_cardPoolLabel);
        _changeEventsLabel    = MakeValueLabel(); vbox.AddChild(_changeEventsLabel);
        _lastChangeEventLabel = MakeValueLabel(); vbox.AddChild(_lastChangeEventLabel);
        _reactionTriggerLabel = MakeValueLabel(); vbox.AddChild(_reactionTriggerLabel);
        _reactionDepthLabel   = MakeValueLabel(); vbox.AddChild(_reactionDepthLabel);

        // ── Details toggle ────────────────────────────────────────────────────
        var sep2 = new HSeparator();
        sep2.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(sep2);

        _detailsToggle = new Button();
        _detailsToggle.Text = "Details ▼";
        _detailsToggle.AddThemeFontSizeOverride("font_size", 11);
        _detailsToggle.Pressed += OnDetailsTogglePressed;
        vbox.AddChild(_detailsToggle);


    }

    private void OnDetailsTogglePressed()
    {
        GameStateDetailPanel.Instance?.Toggle();
        bool open = GameStateDetailPanel.Instance?.Visible ?? false;
        _detailsToggle.Text = open ? "Details ▲" : "Details ▼";
    }

    private static Label MakeSectionHeader(string text)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeColorOverride("font_color", new Color(0.7f, 0.85f, 1f, 1f));
        label.AddThemeFontSizeOverride("font_size", 11);
        return label;
    }

    private static Label MakeValueLabel()
    {
        var label = new Label();
        label.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f, 1f));
        label.AddThemeFontSizeOverride("font_size", 11);
        return label;
    }

    // ── Event wiring ──────────────────────────────────────────────────────────

    private void SubscribeToEvents()
    {
        _onNextStepStarted     = (_)  => CallDeferred(MethodName.UpdateDisplay);
        _onNewTurnStarted      = (_)  => CallDeferred(MethodName.UpdateDisplay);
        _onCardPlayStarted     = ()   => CallDeferred(MethodName.UpdateDisplay);
        _onCardPlayPoolFinished= ()   => CallDeferred(MethodName.UpdateDisplay);
        _onGameChangeEventAfter= (_)  => CallDeferred(MethodName.UpdateDisplay);

        EventBus.Instance.NextStepStarted       += _onNextStepStarted;
        EventBus.Instance.NewTurnStarted         += _onNewTurnStarted;
        EventBus.Instance.CardPlayStarted        += _onCardPlayStarted;
        EventBus.Instance.CardPlayPoolFinished   += _onCardPlayPoolFinished;
        EventBus.Instance.GameChangeEventAfter   += _onGameChangeEventAfter;
    }

    // ── Display update ────────────────────────────────────────────────────────

    private void UpdateDisplay()
    {
        UpdateGameFlowSection();
        UpdateCardPlayRoundSection();
    }

    private void UpdateGameFlowSection()
    {
        var gf = GameFlow.Instance;
        if (gf == null)
        {
            _turnLabel.Text        = "Turn:         —";
            _roundLabel.Text       = "Round:        —";
            _turnStepLabel.Text    = "Step:         —";
            _factionLabel.Text     = "Faction:      —";
            _stepCounterLabel.Text = "StepCounter:  —";
            return;
        }

        _turnLabel.Text        = $"Turn:         {gf.GameTurn}";
        _roundLabel.Text       = $"Round:        {gf.Round}";
        _turnStepLabel.Text    = $"Step:         {gf.TurnStep}";
        _factionLabel.Text     = $"Faction:      {gf.CurrentFaction}";
        _stepCounterLabel.Text = $"StepCounter:  {gf.TurnStepCounter}";
    }

    private void UpdateCardPlayRoundSection()
    {
        var cpr = CardPlayRound.Current;
        if (cpr == null)
        {
            _cardPoolLabel.Text        = "CardPool:     —";
            _changeEventsLabel.Text    = "ChangeEvents: —";
            _lastChangeEventLabel.Text = "LastEvent:    —";
            _reactionTriggerLabel.Text = "RxTrigger:    —";
            _reactionDepthLabel.Text   = "RxDepth:      —";
            return;
        }

        _cardPoolLabel.Text        = $"CardPool:     {cpr.CardPool.Count}";
        _changeEventsLabel.Text    = $"ChangeEvents: {cpr.ChangeEventsPool.Count}";
        _lastChangeEventLabel.Text = $"LastEvent:    {cpr.LastChangeEvent?.GetType().Name ?? "—"}";
        _reactionTriggerLabel.Text = $"RxTrigger:    {cpr.CurrentReactionTrigger?.GetType().Name ?? "—"}";
        _reactionDepthLabel.Text   = $"RxDepth:      {cpr.ReactionDepth}";
    }

}
