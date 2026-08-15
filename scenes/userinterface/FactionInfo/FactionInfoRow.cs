using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public partial class FactionInfoRow : Control
{
	public Faction Faction;
	private FactionState FactionState => FactionState.ForEnum(Faction);
	private MultiplayerGameState gameState { get { return GameSession.Current.GameState; } }
	private Panel BackgroundPanel => GetNode<Panel>("%BackgroundPanel");
	private TextureRect FactionFlagNode => GetNode<TextureRect>("FactionFlag");    
	private Label ScoreLabel => FactionFlagNode.GetNode<Label>("ScoreLabel");
	private Panel DetailPanel => GetNode<Panel>("DetailPanel");    
	private Button FactionInfoButton => GetNode<Button>("FactionInfoButton");
	private Button DiscardDeckButton => BackgroundPanel.GetNode<Button>("DiscardDeckButton");
	private Button PlayedCardsButton => BackgroundPanel.GetNode<Button>("PlayedCardsButton");
	private RichTextLabel TurnSummariesRichText => DetailPanel.GetNode<RichTextLabel>("MarginContainer/TurnSummariesRichText");
	private CardsAnimationControl CardsAnimationControl => GetNode<CardsAnimationControl>("CardsAnimationControl");

	private Timer hoverTimer;
	private bool isMouseOver = false;

	// Store event handlers for cleanup

	public override void _Ready()
	{   
		EventBus.Instance.NewTurnStarted += OnNewTurnStarted; // Update modulation at the start of each turn to reflect current faction        
		LoadUI();

	}
	public override void _ExitTree()
	{
		// Unsubscribe from events
		if (EventBus.Instance != null)
		{
			EventBus.Instance.NewTurnStarted -= OnNewTurnStarted;
			EventBus.Instance.FactionScoredPoints -= OnFactionScoredPoints;
			PlayedCardsButton.Pressed -= OnPlayedCardsButtonPressed;
			FactionInfoButton.Pressed -= OnFactionInfoButtonPressed;
			DiscardDeckButton.Pressed -= OnDiscardDeckButtonPressed;
		}
	}

	private void OnNewTurnStarted(int turnNumber)
	{
		SetModulation();
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

		EventBus.Instance.FactionScoredPoints += OnFactionScoredPoints;                
		PlayedCardsButton.Pressed += OnPlayedCardsButtonPressed;
		FactionInfoButton.Pressed += OnFactionInfoButtonPressed;
		DiscardDeckButton.Pressed += OnDiscardDeckButtonPressed;
		SetModulation();
	}

	/**
	* Event Handlers   
	*/

	private void OnFactionScoredPoints(Faction faction, int newScore)
	{
		if (faction == Faction)
		{
			SetScore(newScore);
		}
	}

	private void OnFactionInfoButtonPressed()
	{
		ToggleDetails();
	}

	private void OnPlayedCardsButtonPressed()
	{
		DeckState deckState = DeckState.ForFaction(Faction);
		List<int> playedCardIds = [.. deckState.StatusCardIds, .. deckState.ResponseCardIds];
		List<PresentationItem> presentationItems = (List<PresentationItem>)PresentationItemCard.FromCardIds(playedCardIds, false);            
		PresentationModal.Current.ShowModal(presentationItems, $"{FactionState.FactionData.FactionAdjactiveLabel} Played Cards", false);
	}
	
	private void OnDiscardDeckButtonPressed()
	{   
		List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(DeckState.ForFaction(Faction).DiscardedCardIds, false);
		PresentationModal.Current.ShowModal(presentationItems, $"{FactionState.FactionData.FactionAdjactiveLabel} Discard Deck", false);
	}
	private void SetModulation()
	{        
		if (PlayerFactionRegistry.GetLocalPlayerFactions().Contains(Faction))
		{            
			Modulate = new Color(1, 1, 1, 1f); // Full opacity for factions controlled by the local player
		} 
		else
		{
			Modulate = new Color(0.5f, 0.5f, 0.5f, 1f); // Dim the row for factions not controlled by the local player
		}
		if (GameFlow.Instance.CurrentFaction != Faction)
		{
			Modulate = new Color(Modulate.R - 0.2f, Modulate.G - 0.2f, Modulate.B - 0.2f,  Modulate.A); // Further dim the row if it's not the current faction's turn
		}
	}

	private void SetScore(int score)
	{
		ScoreLabel.Text = score.ToString();
		var tween = GetTree().CreateTween();
		tween.TweenProperty(ScoreLabel.LabelSettings, "font_size", 72, GameSettings.DurationShortSeconds);
		tween.TweenProperty(ScoreLabel.LabelSettings, "font_size", 36, GameSettings.DurationShortSeconds);
		LoadVPDetails();
	}

	private void ToggleDetails()
	{
		DetailPanel.Visible = !DetailPanel.Visible;
		if (DetailPanel.Visible)
		{            
			LoadVPDetails();            
		}
	}

	private void LoadVPDetails()
	{
		TurnSummariesRichText.Text = string.Empty;
		var textRows = new List<string>();
		var vpSummaries = GameFlow.Instance.VictoryPointSummaries.GetValueOrDefault(Faction, new List<VPTurnSummary>());

		// Grouped by round, not by turn: a faction can pick up points outside its own turn (a card
		// scoring on a reaction, or the empty-deck discard penalty during the attacker's turn), so
		// one round can hold several summaries. Rounds are what the end-of-game screen reports too.
		foreach (IGrouping<int, VPTurnSummary> round in vpSummaries.GroupBy(s => s.Round).OrderBy(g => g.Key))
		{
			textRows.Add($"Round {round.Key} — {round.Sum(s => s.TotalScore)} VP");
			foreach (VPEntry victoryPointEntry in round.SelectMany(s => s.victoryPointEntries))
			{
				textRows.Add(victoryPointEntry.Description);
			}
		}

		TurnSummariesRichText.Text = string.Join("\n", textRows);
	}

	public void ShowCardDelta(int delta)
	{
		CardsAnimationControl.ShowCardsAnimation(delta);
	}
}
