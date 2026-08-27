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
	private Panel BackgroundPanel2 => GetNode<Panel>("%BackgroundPanel2");
	private Panel FactionFlagPanel => GetNode<Panel>("%FlagPanel");    
	private Label ScoreLabel => FactionFlagPanel.GetNode<Label>("ScoreLabel");
	private Panel DetailPanel => GetNode<Panel>("DetailPanel");    
	private Button FactionInfoButton => GetNode<Button>("FactionInfoButton");
	private Button DiscardDeckButton => BackgroundPanel.GetNode<Button>("DiscardDeckButton");
	private Label DeckCardNumber => DiscardDeckButton.GetNode<Label>("DeckCardNumber");
	private Button PlayedCardsButton => BackgroundPanel.GetNode<Button>("PlayedCardsButton");
	private RichTextLabel TurnSummariesRichText => DetailPanel.GetNode<RichTextLabel>("MarginContainer/TurnSummariesRichText");
	private CardsAnimationControl CardsAnimationControl => GetNode<CardsAnimationControl>("CardsAnimationControl");
	private TextureRect ArmyIcon => GetNode<TextureRect>("%ArmyIcon");
	private TextureRect NavyIcon => GetNode<TextureRect>("%NavyIcon");
	private Label ArmyCountLabel => GetNode<Label>("%ArmyCountLabel");
	private Label NavyCountLabel => GetNode<Label>("%NavyCountLabel");
	private ReactionSkipToggleButton ReactionSkipToggleButton => GetNode<ReactionSkipToggleButton>("%ReactionSkipToggleButton");

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
			EventBus.Instance.UnitDeployed -= OnUnitDeployed;
			EventBus.Instance.UnitRemoved -= OnUnitRemoved;
			EventBus.Instance.GameStateRecalculated -= SetUnitCounts;
			EventBus.Instance.GameStateRecalculated -= SetDeckCardCount;
			EventBus.Instance.CardsDrawn -= OnCardsChanged;
			EventBus.Instance.CardsDiscarded -= OnCardsChanged;
			EventBus.Instance.ReactionSkipPreferenceChanged -= OnReactionSkipPreferenceChanged;
			EventBus.Instance.NextStepStarted -= OnNextStepStarted;
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
		BackgroundPanel2.SelfModulate = Colors.White;

		// The flag lives in the panel's StyleBoxTexture, and that stylebox is shared by every instance
		// of this scene - duplicate it first, or the last row painted would set the flag for all of them.
		StyleBoxTexture flagStyleBox = (StyleBoxTexture)FactionFlagPanel.GetThemeStylebox("panel").Duplicate();
		flagStyleBox.Texture = FactionState.FactionData.FlagTexture;
		FactionFlagPanel.AddThemeStyleboxOverride("panel", flagStyleBox);


		StyleBoxFlat styleBox = (StyleBoxFlat)BackgroundPanel.GetThemeStylebox("panel").Duplicate();
		styleBox.BgColor = factionColor;
		BackgroundPanel.AddThemeStyleboxOverride("panel", styleBox);
		BackgroundPanel2.AddThemeStyleboxOverride("panel", styleBox);

		StyleBoxFlat styleBoxDetails = (StyleBoxFlat)DetailPanel.GetThemeStylebox("panel").Duplicate();
		styleBoxDetails.BgColor = factionColor;
		DetailPanel.AddThemeStyleboxOverride("panel", styleBoxDetails);

		ScoreLabel.LabelSettings = (LabelSettings)ScoreLabel.LabelSettings.Duplicate();

		// SelfModulate, not Modulate: the row-level Modulate dimming in SetModulation() has to compose
		// on top of the faction tint, same reason BackgroundPanel uses SelfModulate above.
		ArmyIcon.SelfModulate = factionColor;
		NavyIcon.SelfModulate = factionColor;

		// Each count label recolours independently, so neither may share a LabelSettings instance.
		ArmyCountLabel.LabelSettings = (LabelSettings)ArmyCountLabel.LabelSettings.Duplicate();
		NavyCountLabel.LabelSettings = (LabelSettings)NavyCountLabel.LabelSettings.Duplicate();

		ReactionSkipToggleButton.Faction = Faction;
		ReactionSkipToggleButton.Refresh();

		SetScore(FactionState.Score);
		SetUnitCounts();
		SetDeckCardCount();

		EventBus.Instance.FactionScoredPoints += OnFactionScoredPoints;
		EventBus.Instance.UnitDeployed += OnUnitDeployed;
		EventBus.Instance.UnitRemoved += OnUnitRemoved;
		EventBus.Instance.GameStateRecalculated += SetUnitCounts;
		EventBus.Instance.GameStateRecalculated += SetDeckCardCount;
		EventBus.Instance.CardsDrawn += OnCardsChanged;
		EventBus.Instance.CardsDiscarded += OnCardsChanged;
		EventBus.Instance.ReactionSkipPreferenceChanged += OnReactionSkipPreferenceChanged;
		// NextStepStarted, not NewTurnStarted: the latter is emitted inside GameFlow.StartNewTurn, which
		// only the host runs. TurnStepCounter is a replicated property whose setter emits this on every
		// peer, so it is the one turn-boundary signal a client can rely on — and it is exactly when a
		// TURN_STEP arming expires and the toggle has to fall back to "always ask".
		EventBus.Instance.NextStepStarted += OnNextStepStarted;
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

	private void OnUnitDeployed(int unitId, int countryId)
	{
		if (UnitState.ForId(unitId).Faction == Faction)
		{
			SetUnitCounts();
		}
	}

	private void OnUnitRemoved(int unitId, int countryId)
	{
		if (UnitState.ForId(unitId).Faction == Faction)
		{
			SetUnitCounts();
		}
	}

	/// <summary>
	/// Handles both CardsDrawn and CardsDiscarded — they share the (faction, numberOfCards) shape.
	/// Gives immediate feedback for the common case; GameStateRecalculated is the catch-all resync.
	/// </summary>
	private void OnCardsChanged(int faction, int numberOfCards)
	{
		if ((Faction)faction == Faction)
		{
			SetDeckCardCount();
		}
	}

	private void OnReactionSkipPreferenceChanged(int faction)
	{
		if ((Faction)faction == Faction) ReactionSkipToggleButton.Refresh();
	}

	/// <summary>
	/// A TURN_STEP or ROUND arming can expire without anyone touching the toggle, so repaint it at
	/// every step boundary rather than only when the setting is changed.
	/// </summary>
	private void OnNextStepStarted(int turnStep)
	{
		ReactionSkipToggleButton.Refresh();
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
		_ = ModalStack.Current.Show(
			ModalConfig.Display($"{FactionState.FactionData.FactionAdjactiveLabel} Played Cards", presentationItems)
				.WithDedupeKey($"played-cards:{Faction}"));
	}
	
	private void OnDiscardDeckButtonPressed()
	{   
		List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(DeckState.ForFaction(Faction).DiscardedCardIds, false);
		_ = ModalStack.Current.Show(
			ModalConfig.Display($"{FactionState.FactionData.FactionAdjactiveLabel} Discard Deck", presentationItems)
				.WithDedupeKey($"discard-deck:{Faction}"));
	}
	private void SetModulation()
	{        
		bool isLocal = PlayerFactionRegistry.GetLocalPlayerFactions().Contains(Faction);
		// Re-tested here rather than once in LoadUI so a rejoin or a faction reassignment is picked up.
		// A setting on a faction this peer does not control would do nothing, and hiding it leaves the
		// other five rows exactly as they were.
		ReactionSkipToggleButton.Visible = isLocal;

		if (isLocal)
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

	/// <summary>
	/// Repaints the army/navy pool counters — how many pieces of each type the faction has left to play.
	/// </summary>
	private void SetUnitCounts()
	{
		SetUnitCount(ArmyCountLabel, UnitPool.AvailableUnitCount(Faction, UnitType.ARMY));
		SetUnitCount(NavyCountLabel, UnitPool.AvailableUnitCount(Faction, UnitType.NAVY));
	}

	/// <summary>
	/// Repaints the draw-deck counter — how many cards the faction has left to draw.
	/// Reads state directly rather than tracking deltas: several paths change DeckCardIds without
	/// emitting CardsDrawn/CardsDiscarded (DiscardTopCards, PlayCard, ShuffleDeck, RecycleCardChangeEvent,
	/// and a client applying a snapshot), so the card signals alone would drift out of sync.
	/// </summary>
	private void SetDeckCardCount()
	{
		// FactionState.ForEnum returns null before the game state exists — same guard as UnitPool.AvailableUnitCount.
		DeckState deckState = FactionState?.DeckState;
		DeckCardNumber.Text = (deckState?.DeckCardIds.Count ?? 0).ToString();
	}

	private void SetUnitCount(Label label, int count)
	{		
		label.Text = count.ToString();
		// FontColor on the LabelSettings, not AddThemeColorOverride: a Label with label_settings
		// assigned ignores theme colour overrides. The black border comes from the same resource.
		label.LabelSettings.FontColor = count switch
		{
			0 => Colors.Red,
			1 => Colors.Orange,
			_ => Colors.White
		};
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
