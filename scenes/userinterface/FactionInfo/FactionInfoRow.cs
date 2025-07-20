using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public partial class FactionInfoRow : Control
{
    [Export] public Faction Faction;

    private FactionState FactionState => gameState.FactionStates[Faction]; 

    private GameFlow gameFlow { get { return GameSession.Instance.GameFlow; } }
    private GameState gameState { get { return GameSession.Instance.GameState; } }

    private Panel BackgroundPanel => GetNode<Panel>("%BackgroundPanel");
    private Label ScoreLabel => GetNode<Label>("%ScoreLabel");
    private Panel DetailPanel => GetNode<Panel>("%DetailPanel");
    private RichTextLabel TurnSummariesRichText => GetNode<RichTextLabel>("%TurnSummariesRichText");
    private Button FactionInfoButton => GetNode<Button>("%FactionInfoButton");
    private Button DeckButton => GetNode<Button>("%DeckButton");
    private Button PlayedCardsButton => GetNode<Button>("%PlayedCardsButton");
    private TextureRect FactionFlagNode => GetNode<TextureRect>("%FactionFlag");    

    private Timer hoverTimer;
    private bool isMouseOver = false;

    public override void _Ready()
    {
        EventBus.Instance.GameSessionStarted += () => { this.LoadUI(); };
    }

    /**
    * Different LoadUI than LoadableUI
    */
    public void LoadUI()
    {
        // Set background color
        Color factionColor = FactionState.FactionData.FactionColor;        
        BackgroundPanel.SelfModulate = Colors.White;

        Texture2D factionFlag = FactionState.FactionData.FlagTexture;
        FactionFlagNode.Texture = factionFlag;


        StyleBoxFlat styleBox = (StyleBoxFlat)BackgroundPanel.GetThemeStylebox("panel").Duplicate();
        styleBox.BgColor = factionColor;
        BackgroundPanel.AddThemeStyleboxOverride("panel", styleBox);

        StyleBoxFlat styleBoxDetails = (StyleBoxFlat)DetailPanel.GetThemeStylebox("panel").Duplicate();
        styleBoxDetails.BgColor = factionColor;
        DetailPanel.AddThemeStyleboxOverride("panel", styleBoxDetails);

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
            if (Faction != (Faction)faction)
            {
                DetailPanel.Visible = false; 
            }
        };

        DeckButton.Pressed += () =>
        {
            List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(DeckState.ForFaction(Faction).DeckCardIds, false);
            PresentationModal.Instance.ShowModal(presentationItems, $"{FactionState.FactionData.FactionAdjactiveLabel} Draw Deck", false);
        };
        PlayedCardsButton.Pressed += () =>
        {
            DeckState deckState = DeckState.ForFaction(Faction);
            List<int> playedCardIds = [.. deckState.StatusCardIds, .. deckState.ResponseCardIds];
            List<PresentationItem> presentationItems = (List<PresentationItem>)PresentationItemCard.FromCardIds(playedCardIds, false);            
            PresentationModal.Instance.ShowModal(presentationItems, $"{FactionState.FactionData.FactionAdjactiveLabel} Played Cards", false);
        };

        FactionInfoButton.Pressed += () => { ToggleDetails(); };
    }

    private void SetScore(int score)
    {
        ScoreLabel.Text = score.ToString();
        var tween = GetTree().CreateTween();
        tween.TweenProperty(ScoreLabel.LabelSettings, "font_size", 72, 0.2);
        tween.TweenProperty(ScoreLabel.LabelSettings, "font_size", 36, 0.2);
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
            foreach (VPEntry victoryPointEntry in summary.victoryPointEntries)
            {
                textRows.Add($"{victoryPointEntry.VictoryPoints} points for {victoryPointEntry.Reason}");
            }
        }

        TurnSummariesRichText.Text = string.Join("\n", textRows);
    }
}
