using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class FactionInfoRow : Control
{
    [Export]
    public Faction Faction;

    private FactionState FactionState => gameState.FactionStates[Faction]; 

    private GameFlow gameFlow { get { return GameSession.Instance.GameFlow; } }
    private GameState gameState { get { return GameSession.Instance.GameState; } }

    private Panel BackgroundPanel => GetNode<Panel>("%BackgroundPanel");
    private Label ScoreLabel => GetNode<Label>("%ScoreLabel");
    private Label DetailPanel => GetNode<Label>("%DetailPanel");
    private RichTextLabel TurnSummariesRichText => GetNode<RichTextLabel>("%TurnSummariesRichText");

    private Timer hoverTimer;
    private bool isMouseOver = false;

    public override void _Ready()
    {
        // Set background color
        Color factionColor = FactionState.FactionData.FactionColor;
        BackgroundPanel.SelfModulate = Colors.White;

        StyleBoxFlat styleBox = (StyleBoxFlat)BackgroundPanel.GetThemeStylebox("panel").Duplicate();
        styleBox.BgColor = factionColor;
        BackgroundPanel.AddThemeStyleboxOverride("panel", styleBox);

        ScoreLabel.LabelSettings = (LabelSettings)ScoreLabel.LabelSettings.Duplicate();

        SetScore(FactionState.Score);

        EventBus.Instance.FactionScoredPoints += (faction, newScore) =>
        {
            if (Faction == faction)
            {
                SetScore(newScore);
                LoadVPDetails();
            }
        };

        EventBus.Instance.VpDetailsPanelOpened += (faction) =>
        {
            if (Faction == faction)
            {
                DetailPanel.Visible = false;
            }
        };
    }

    private void SetScore(int score)
    {
        ScoreLabel.Text = score.ToString();
        var tween = GetTree().CreateTween();
        tween.TweenProperty(ScoreLabel.LabelSettings, "font_size", 72, 0.2);
        tween.TweenProperty(ScoreLabel.LabelSettings, "font_size", 36, 0.2);
    }

    private void OnMouseEntered()
    {
        isMouseOver = true;
    }

    private void OnMouseExited()
    {
        isMouseOver = false;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (isMouseOver && @event is InputEventMouseButton mouseButtonEvent && mouseButtonEvent.IsReleased())
        {
            ToggleDetails();
        }
    }

    private void ToggleDetails()
    {
        DetailPanel.Visible = !DetailPanel.Visible;
        if (DetailPanel.Visible)
        {
            EventBus.Emit(EventBus.SignalName.VpDetailsPanelOpened,(int)Faction);
            LoadVPDetails();
        }
    }

    private void LoadVPDetails()
    {
        TurnSummariesRichText.Text = string.Empty;
        var textRows = new List<string>();
        var vpSummaries = gameFlow.VictoryPointSummaries[Faction];

        foreach (VPTurnSummary summary in vpSummaries)
        {
            textRows.Add($"Turn: {summary.TurnNumber}");
            foreach (var kvp in summary.ScoresForReason)
            {
                textRows.Add($"{kvp.Value} points for {kvp.Key}");
            }
        }

        TurnSummariesRichText.Text = string.Join("\n", textRows);
    }
}
