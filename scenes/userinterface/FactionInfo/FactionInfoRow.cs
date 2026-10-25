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
	private Label PlayedCardNumber => PlayedCardsButton.GetNode<Label>("PlayedCardsNumber");
	private RichTextLabel TurnSummariesRichText => DetailPanel.GetNode<RichTextLabel>("MarginContainer/TurnSummariesRichText");
	private CardsAnimationControl CardsAnimationControl => GetNode<CardsAnimationControl>("CardsAnimationControl");
	private TextureRect ArmyIcon => GetNode<TextureRect>("%ArmyIcon");
	private TextureRect NavyIcon => GetNode<TextureRect>("%NavyIcon");
	private Label ArmyCountLabel => GetNode<Label>("%ArmyCountLabel");
	private Label NavyCountLabel => GetNode<Label>("%NavyCountLabel");

	private Timer hoverTimer;
	private bool isMouseOver = false;

	// Store event handlers for cleanup

	public override void _Ready()
	{
		LoadUI();
	}
	public override void _ExitTree()
	{
		// Unsubscribe from events
		if (EventBus.Instance != null)
		{
			EventBus.Instance.FactionScoredPoints -= OnFactionScoredPoints;
			EventBus.Instance.UnitDeployed -= OnUnitDeployed;
			EventBus.Instance.UnitRemoved -= OnUnitRemoved;
			EventBus.Instance.GameStateRecalculated -= SetUnitCounts;
			EventBus.Instance.GameStateRecalculated -= SetCardCounts;
			EventBus.Instance.CardsDrawn -= OnCardsChanged;
			EventBus.Instance.CardsDiscarded -= OnCardsChanged;
			EventBus.Instance.NextStepStarted -= OnNextStepStarted;
			EventBus.Instance.GameChangeEventAfter -= OnGameChangeEventAfter;
			PlayedCardsButton.Pressed -= OnPlayedCardsButtonPressed;
			FactionInfoButton.Pressed -= OnFactionInfoButtonPressed;
			DiscardDeckButton.Pressed -= OnDiscardDeckButtonPressed;
		}
	}

	/// <summary>
	/// The dim state below is derived from GameFlow.CurrentFaction, so it has to repaint wherever the
	/// turn can move — and NOT on NewTurnStarted, which is where this used to hang: that is emitted
	/// inside GameFlow.StartNewTurn, which only the host runs, so a joined client sat on the opening
	/// faction's dimming for the whole game and a save restore never repainted at all. Same pair, and
	/// same reasoning, as RoundTrack and TurnFactionTint.
	/// </summary>
	private void OnGameChangeEventAfter(string changeEventName)
	{
		SetModulation();
		// Also the catch-all for the two card counters: a change event that declares RecalcScope.None
		// never reaches GameStateRecalculated, and two label writes are cheap enough to do unconditionally.
		SetCardCounts();
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

		// The minor factions this faction acts for share its row: their points fold into its score and
		// their pieces in reserve hang in a tab under it, one per minor faction.
		_minors = StaticGameData.MinorsOf(Faction);
		AddAirCell();
		AddMinorTabs();

		SetScore(CombinedScore);
		SetUnitCounts();
		SetCardCounts();

		EventBus.Instance.FactionScoredPoints += OnFactionScoredPoints;
		EventBus.Instance.UnitDeployed += OnUnitDeployed;
		EventBus.Instance.UnitRemoved += OnUnitRemoved;
		EventBus.Instance.GameStateRecalculated += SetUnitCounts;
		EventBus.Instance.GameStateRecalculated += SetCardCounts;
		EventBus.Instance.CardsDrawn += OnCardsChanged;
		EventBus.Instance.CardsDiscarded += OnCardsChanged;
		// NextStepStarted, not NewTurnStarted: the latter is emitted inside GameFlow.StartNewTurn, which
		// only the host runs. TurnStepCounter is a replicated property whose setter emits this on every
		// peer, so it is the one turn-boundary signal a client can rely on.
		EventBus.Instance.NextStepStarted += OnNextStepStarted;
		EventBus.Instance.GameChangeEventAfter += OnGameChangeEventAfter;
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
		if (IsShownHere(faction))
		{
			SetScore(CombinedScore);
		}
	}

	private void OnUnitDeployed(int unitId, int countryId)
	{
		if (IsShownHere(UnitState.ForId(unitId).Faction))
		{
			SetUnitCounts();
		}
	}

	private void OnUnitRemoved(int unitId, int countryId)
	{
		if (IsShownHere(UnitState.ForId(unitId).Faction))
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
			SetCardCounts();
		}
	}

	/// <summary>
	/// Repaints the dimming: this is the one turn-boundary signal that reaches a client after a save
	/// restore, where the turn has already moved before any new event lands.
	/// </summary>
	private void OnNextStepStarted(int turnStep)
	{
		SetModulation();
	}

	private void OnFactionInfoButtonPressed()
	{
		ToggleDetails();
	}

	private void OnPlayedCardsButtonPressed()
	{
		DeckState deckState = DeckState.ForFaction(Faction);
		List<int> playedCardIds = [.. deckState.StatusCardIds, .. deckState.ResponseCardIds];
		List<PresentationItem> presentationItems = playedCardIds
			.Select(cardId => (PresentationItem)new PresentationItemCard(cardId, false) { DimCaption = IgnoredCaption(cardId) })
			.ToList();
		_ = ModalStack.Current.Show(
			ModalConfig.Display($"{FactionState.FactionData.FactionAdjactiveLabel} Active Cards", presentationItems)
				.WithDedupeKey($"played-cards:{Faction}"));
	}
	
	/// <summary>"Ignored (Hobart's Funnies)" while a card in play ignores this one, else null: the table dims it.</summary>
	private static string IgnoredCaption(int cardId) =>
		BoardState.Live.IgnoredBy(CardState.ForId(cardId).CardLogic) is { } by ? $"Ignored ({by.CardData.Label})" : null;

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
		if (isLocal)
		{            
			Modulate = new Color(1, 1, 1, 1f); // Full opacity for factions controlled by the local player
		} 
		else
		{
			Modulate = new Color(0.5f, 0.5f, 0.5f, 1f); // Dim the row for factions not controlled by the local player
		}
		// Guarded because GameChangeEventAfter can now reach this before GameFlow exists on a peer.
		if (GameFlow.Instance != null && GameFlow.Instance.CurrentFaction != Faction)
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
	/// Repaints the army/navy (and air) pool counters — how many pieces of each type the faction has left to play.
	/// </summary>
	private void SetUnitCounts()
	{
		SetUnitCount(ArmyCountLabel, UnitPool.AvailableUnitCount(Faction, UnitType.ARMY));
		SetUnitCount(NavyCountLabel, UnitPool.AvailableUnitCount(Faction, UnitType.NAVY));
		foreach (((Faction faction, UnitType type), Label label) in _builtCountLabels)
			SetUnitCount(label, UnitPool.AvailableUnitCount(faction, type));
	}

	/// <summary>
	/// Repaints the draw-deck counter (cards left to draw) and the active-cards counter (status and
	/// response cards currently on the table).
	/// Reads state directly rather than tracking deltas: several paths change DeckCardIds without
	/// emitting CardsDrawn/CardsDiscarded (DiscardTopCards, PlayCard, ShuffleDeck, RecycleCardChangeEvent,
	/// and a client applying a snapshot), so the card signals alone would drift out of sync.
	/// </summary>
	private void SetCardCounts()
	{
		// FactionState.ForEnum returns null before the game state exists — same guard as UnitPool.AvailableUnitCount.
		DeckState deckState = FactionState?.DeckState;
		DeckCardNumber.Text = (deckState?.DeckCardIds.Count ?? 0).ToString();
		// Active cards are the status and response cards still lying on the table — the same two piles
		// the Active Cards modal lists, so the counter can never disagree with what the button opens.
		int activeCardCount = (deckState?.StatusCardIds.Count ?? 0) + (deckState?.ResponseCardIds.Count ?? 0);
		PlayedCardNumber.Text = activeCardCount.ToString();
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
		// This faction's summaries, then those of the minor factions it acts for, whose points the row's
		// score includes. Within a round they keep that order, so the patron's own lines come first.
		var vpSummaries = _minors.Prepend(Faction)
			.SelectMany(faction => GameFlow.Instance.VictoryPointSummaries.GetValueOrDefault(faction, new List<VPTurnSummary>()));

		// Grouped by round, not by turn: a faction can pick up points outside its own turn (a card
		// scoring on a reaction, or the empty-deck discard penalty during the attacker's turn), so
		// one round can hold several summaries. Rounds are what the end-of-game screen reports too.
		foreach (IGrouping<int, VPTurnSummary> round in vpSummaries.GroupBy(s => s.Round).OrderBy(g => g.Key))
		{
			textRows.Add($"Round {round.Key} — {round.Sum(s => s.TotalScore)} VP");
			foreach (VPTurnSummary summary in round)
			{
				// "(France)" on a minor faction's lines, so the row says whose points they are.
				string whose = summary.Faction == Faction ? string.Empty : $" ({summary.Faction.Label()})";
				foreach (VPEntry victoryPointEntry in summary.victoryPointEntries)
					textRows.Add(victoryPointEntry.Description + whose);
			}
		}

		TurnSummariesRichText.Text = string.Join("\n", textRows);
	}

	/// <summary>Counters built in code rather than in the scene: the Air Force one and the minor-faction tabs'.</summary>
	private readonly Dictionary<(Faction, UnitType), Label> _builtCountLabels = new();

	/// <summary>
	/// A unit icon's width on a row that shows Air Forces: army, navy and air cells have to share the
	/// row's 150px, and the scene's 35px icons and 22px labels fit only two.
	/// </summary>
	private const float CompactIconWidth = 34f;
	private const float CompactLabelWidth = 12f;

	/// <summary>
	/// An air cell after the navy one, when the faction has Air Forces in its pool. Only an expansion
	/// (Total War) gives any, so the base game's strip stays exactly as the scene draws it.
	/// </summary>
	private void AddAirCell()
	{
		if (!_minors.Prepend(Faction).Any(HasAirForces)) return;

		// Narrowed before anything is copied from them, so the minor tabs' counters come out narrow too.
		foreach (Control icon in new Control[] { ArmyIcon, NavyIcon })
			icon.CustomMinimumSize = icon.CustomMinimumSize with { X = CompactIconWidth };
		foreach (Control label in new Control[] { ArmyCountLabel, NavyCountLabel })
			label.CustomMinimumSize = label.CustomMinimumSize with { X = CompactLabelWidth };

		if (!HasAirForces(Faction)) return;
		HBoxContainer airCell = new() { MouseFilter = MouseFilterEnum.Ignore };
		airCell.AddThemeConstantOverride("separation", 2);
		NavyIcon.GetParent().AddSibling(airCell);
		AddCounter(airCell, Faction, UnitType.AIR_FORCE, FactionState.FactionData.FactionColor);
	}

	private static bool HasAirForces(Faction faction) => StaticGameData.FactionDataMap[faction].NumberOfAirUnits > 0;

	// ── Minor factions sharing this row ──────────────────────────────────────

	/// <summary>Height of one minor faction's tab under the row; FactionsContainer makes room for them.</summary>
	public const float MinorTabHeight = 25f;

	private List<Faction> _minors = new();

	/// <summary>This row's faction, or a minor faction it acts for.</summary>
	private bool IsShownHere(Faction faction) => faction == Faction || _minors.Contains(faction);

	/// <summary>The row's headline score: this faction's points plus those of the minor factions it acts for.</summary>
	private int CombinedScore => FactionState.Score + _minors.Sum(minor => FactionState.ForEnum(minor)?.Score ?? 0);

	/// <summary>
	/// One tab per minor faction, hung under the row: its army (and navy and air, if it has any) pieces in reserve,
	/// on its own colour, which is what says whose they are. Built in code
	/// from the row's own icons and labels so the scene needs no extra nodes, and the base game, which
	/// has no minor factions, draws exactly what it did.
	/// </summary>
	private void AddMinorTabs()
	{
		float rowHeight = CustomMinimumSize.Y;
		for (int i = 0; i < _minors.Count; i++)
		{
			Faction minor = _minors[i];
			FactionData data = StaticGameData.FactionDataMap[minor];

			// Pass, not Ignore, so the tab can show whose pieces these are on hover.
			Panel tab = new() { MouseFilter = MouseFilterEnum.Pass, TooltipText = $"{data.Label}: pieces in reserve" };
			StyleBoxFlat style = (StyleBoxFlat)BackgroundPanel2.GetThemeStylebox("panel").Duplicate();
			style.BgColor = data.FactionColor;
			tab.AddThemeStyleboxOverride("panel", style);
			tab.Position = new Vector2(0, rowHeight + i * MinorTabHeight);
			tab.Size = new Vector2(CustomMinimumSize.X, MinorTabHeight);
			AddChild(tab);

			HBoxContainer cells = new() { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
			cells.AddThemeConstantOverride("separation", 2);
			cells.SetAnchorsPreset(LayoutPreset.FullRect);
			tab.AddChild(cells);

			AddCounter(cells, minor, UnitType.ARMY, data.FactionColor);
			if (data.NumberOfNavyUnits > 0)
				AddCounter(cells, minor, UnitType.NAVY, data.FactionColor);
			if (data.NumberOfAirUnits > 0)
				AddCounter(cells, minor, UnitType.AIR_FORCE, data.FactionColor);
		}

		// What hangs below the row moves down with it, so the VP popup and the card-delta animation do
		// not cover the tabs.
		float tabsHeight = _minors.Count * MinorTabHeight;
		DetailPanel.OffsetTop += tabsHeight;
		CardsAnimationControl.Position += new Vector2(0, tabsHeight);
	}

	/// <summary>An icon and count for <paramref name="faction"/>'s pool of <paramref name="type"/>, copied from the army counter.</summary>
	private void AddCounter(HBoxContainer cells, Faction faction, UnitType type, Color color)
	{
		TextureRect icon = (TextureRect)ArmyIcon.Duplicate(0);
		icon.UniqueNameInOwner = false;
		icon.Texture = type switch
		{
			UnitType.NAVY => NavyIcon.Texture,
			UnitType.AIR_FORCE => AssetRepository.AirForceSprite,
			_ => ArmyIcon.Texture,
		};
		icon.SelfModulate = color;
		cells.AddChild(icon);

		// Its own LabelSettings: SetUnitCount recolours each counter separately.
		Label label = (Label)ArmyCountLabel.Duplicate(0);
		label.UniqueNameInOwner = false;
		label.LabelSettings = (LabelSettings)ArmyCountLabel.LabelSettings.Duplicate();
		cells.AddChild(label);
		_builtCountLabels[(faction, type)] = label;
	}

	public void ShowCardDelta(int delta)
	{
		CardsAnimationControl.ShowCardsAnimation(delta);
	}

	/// <summary>Where the Active Cards button sits on screen, for a card shown going onto the table.</summary>
	public Rect2 ActiveCardsButtonRect => PlayedCardsButton.GetGlobalRect();
}
