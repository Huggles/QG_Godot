using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// One faction in the top strip: its flag hanging as a cloth banner from a brass rod, with brass tags
/// beside it for cards in hand, in the deck and on the table, and pieces in reserve. The banner hangs
/// lower the more points the faction has, so the strip itself shows who is ahead; the faction whose
/// turn it is flies its banner harder. Clicking the banner opens the score history.
/// </summary>
public partial class FactionInfoRow : Control
{
	public Faction Faction;
	private FactionState FactionState => FactionState.ForEnum(Faction);

	private static readonly Shader ClothShader = GD.Load<Shader>("res://scenes/userinterface/FactionInfo/BannerCloth.gdshader");

	private ColorRect Cloth => GetNode<ColorRect>("%Cloth");
	private Label ScoreLabel => GetNode<Label>("%ScoreLabel");
	private Button FactionInfoButton => GetNode<Button>("%FactionInfoButton");
	private VBoxContainer Tags => GetNode<VBoxContainer>("%Tags");
	private Button HandButton => GetNode<Button>("%HandButton");
	private Button DeckButton => GetNode<Button>("%DeckButton");
	private Button TableButton => GetNode<Button>("%TableButton");
	private PanelContainer ArmyTag => GetNode<PanelContainer>("%ArmyTag");
	private TextureRect ArmyIcon => GetNode<TextureRect>("%ArmyIcon");
	private TextureRect NavyIcon => GetNode<TextureRect>("%NavyIcon");
	private Label ArmyCountLabel => GetNode<Label>("%ArmyCountLabel");
	private Label NavyCountLabel => GetNode<Label>("%NavyCountLabel");
	private CardsAnimationControl CardsAnimationControl => GetNode<CardsAnimationControl>("CardsAnimationControl");

	// ── Banner geometry ──────────────────────────────────────────────────────

	/// <summary>Where the cloth hangs from, just under the rod's top edge.</summary>
	private const float ClothTop = 3f;
	/// <summary>Tail below the flag at zero points: room for the score and the swallowtail.</summary>
	private const float MinTail = 40f;
	/// <summary>Extra tail per point, and the longest it gets, so a runaway leader never curtains the map.</summary>
	private const float TailPerPoint = 1.6f;
	private const float MaxTail = 110f;
	private const float Notch = 16f;

	private static readonly Color TagInk = new(0.255f, 0.141f, 0.008f);

	private ShaderMaterial _cloth;
	private float _flagHeight;
	private float _tail = MinTail;
	private Tween _tailTween;
	private int _shownScore = int.MinValue;

	/// <summary>Length of the tail below the flag; setting it re-lays the cloth, the score and the click area.</summary>
	public float Tail
	{
		get => _tail;
		set
		{
			_tail = value;
			Vector2 size = new(Cloth.Size.X, _flagHeight + _tail);
			Cloth.Size = size;
			_cloth.SetShaderParameter("size", size);
			FactionInfoButton.Size = size;
			ScoreLabel.Position = new Vector2(0, ClothTop + size.Y - Notch - ScoreLabel.Size.Y + 6);
		}
	}

	public override void _Ready()
	{
		LoadUI();
	}

	public override void _ExitTree()
	{
		if (EventBus.Instance != null)
		{
			EventBus.Instance.FactionScoredPoints -= OnFactionScoredPoints;
			EventBus.Instance.UnitDeployed -= OnUnitDeployed;
			EventBus.Instance.UnitRemoved -= OnUnitRemoved;
			EventBus.Instance.GameStateRecalculated -= SetUnitCounts;
			EventBus.Instance.GameStateRecalculated -= SetCardCounts;
			EventBus.Instance.GameStateRecalculated -= RefreshScore;
			EventBus.Instance.CardsDrawn -= OnCardsChanged;
			EventBus.Instance.CardsDiscarded -= OnCardsChanged;
			EventBus.Instance.NextStepStarted -= OnNextStepStarted;
			EventBus.Instance.GameChangeEventAfter -= OnGameChangeEventAfter;
		}
	}

	/// <summary>
	/// The dim state is derived from GameFlow.CurrentFaction, so it has to repaint wherever the turn can
	/// move — and NOT on NewTurnStarted, which only the host emits. Same pair as RoundTrack and TurnFactionTint.
	/// </summary>
	private void OnGameChangeEventAfter(string changeEventName)
	{
		SetModulation();
		// Also the catch-all for the card counters: a change event that declares RecalcScope.None
		// never reaches GameStateRecalculated, and three label writes are cheap enough to do unconditionally.
		SetCardCounts();
	}

	public void LoadUI()
	{
		FactionData data = FactionState.FactionData;
		Color factionColor = data.FactionColor;

		// One material per row: every banner has its own flag, length and sway.
		Texture2D flag = data.FlagTexture;
		_flagHeight = Mathf.Round(Cloth.Size.X * flag.GetHeight() / flag.GetWidth());
		_cloth = new ShaderMaterial { Shader = ClothShader };
		_cloth.SetShaderParameter("flag", flag);
		_cloth.SetShaderParameter("tail_color", factionColor);
		_cloth.SetShaderParameter("flag_height", _flagHeight);
		_cloth.SetShaderParameter("notch", Notch);
		_cloth.SetShaderParameter("phase", GD.Randf() * 10f);
		Cloth.Material = _cloth;

		ScoreLabel.LabelSettings = (LabelSettings)ScoreLabel.LabelSettings.Duplicate();
		ScoreLabel.LabelSettings.FontSize = 32;
		Tail = MinTail;

		FactionInfoButton.TooltipText = $"{data.FactionAdjactiveLabel} score history";
		FactionInfoButton.MouseEntered += () => _cloth.SetShaderParameter("hover", 1f);
		FactionInfoButton.MouseExited += () => _cloth.SetShaderParameter("hover", 0f);
		HandButton.TooltipText = "Cards in hand";
		DeckButton.TooltipText = "Cards left in the draw deck\nClick to see the discard pile";
		TableButton.TooltipText = "Status and response cards on the table\nClick to see them";
		ArmyTag.TooltipText = "Armies in reserve";
		GetNode<Control>("%NavyTag").TooltipText = "Navies in reserve";

		// SelfModulate, not Modulate: the row-level Modulate dimming in SetModulation() composes on top.
		ArmyIcon.SelfModulate = factionColor;
		NavyIcon.SelfModulate = factionColor;
		// Each count label recolours independently, so neither may share a LabelSettings instance.
		ArmyCountLabel.LabelSettings = (LabelSettings)ArmyCountLabel.LabelSettings.Duplicate();
		NavyCountLabel.LabelSettings = (LabelSettings)NavyCountLabel.LabelSettings.Duplicate();

		// The minor factions this faction acts for share its row: their points fold into its score and
		// their pieces in reserve hang as extra tags in their own colour.
		_minors = StaticGameData.MinorsOf(Faction);
		if (HasAirForces(Faction)) AddCounterTag(Faction, UnitType.AIR_FORCE, null);
		AddMinorTags();

		RefreshScore();
		SetUnitCounts();
		SetCardCounts();

		EventBus.Instance.FactionScoredPoints += OnFactionScoredPoints;
		EventBus.Instance.UnitDeployed += OnUnitDeployed;
		EventBus.Instance.UnitRemoved += OnUnitRemoved;
		EventBus.Instance.GameStateRecalculated += SetUnitCounts;
		EventBus.Instance.GameStateRecalculated += SetCardCounts;
		// A snapshot or save restore sets FactionState.Score without a FactionScoredPoints.
		EventBus.Instance.GameStateRecalculated += RefreshScore;
		EventBus.Instance.CardsDrawn += OnCardsChanged;
		EventBus.Instance.CardsDiscarded += OnCardsChanged;
		// NextStepStarted, not NewTurnStarted: TurnStepCounter is a replicated property whose setter
		// emits this on every peer, so it is the one turn-boundary signal a client can rely on.
		EventBus.Instance.NextStepStarted += OnNextStepStarted;
		EventBus.Instance.GameChangeEventAfter += OnGameChangeEventAfter;
		TableButton.Pressed += OnPlayedCardsButtonPressed;
		FactionInfoButton.Pressed += OnFactionInfoButtonPressed;
		DeckButton.Pressed += OnDiscardDeckButtonPressed;
		SetModulation();
	}

	// ── Event handlers ───────────────────────────────────────────────────────

	private void OnFactionScoredPoints(Faction faction, int newScore)
	{
		if (IsShownHere(faction)) RefreshScore();
	}

	private void OnUnitDeployed(int unitId, int countryId)
	{
		if (IsShownHere(UnitState.ForId(unitId).Faction)) SetUnitCounts();
	}

	private void OnUnitRemoved(int unitId, int countryId)
	{
		if (IsShownHere(UnitState.ForId(unitId).Faction)) SetUnitCounts();
	}

	/// <summary>CardsDrawn and CardsDiscarded share the (faction, numberOfCards) shape; GameStateRecalculated is the catch-all.</summary>
	private void OnCardsChanged(int faction, int numberOfCards)
	{
		if ((Faction)faction == Faction) SetCardCounts();
	}

	/// <summary>The one turn-boundary signal that reaches a client after a save restore.</summary>
	private void OnNextStepStarted(int turnStep) => SetModulation();

	private void OnFactionInfoButtonPressed() => ScoreHistoryDialog.Open(GetTree().CurrentScene, Faction);

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

	// ── Painting ─────────────────────────────────────────────────────────────

	private bool IsActive => GameFlow.Instance != null && GameFlow.Instance.IsStarted && GameFlow.Instance.CurrentFaction == Faction;

	/// <summary>
	/// Local factions at full strength, others dimmed, and dimmer still off their turn. The active
	/// faction's banner flies hard and its trim glints; the rest barely stir.
	/// </summary>
	private void SetModulation()
	{
		// Gentler than a flat panel needs: the active banner already stands out by flying, and dimming
		// cloth much further turns the whole strip to mud.
		bool isLocal = PlayerFactionRegistry.GetLocalPlayerFactions().Contains(Faction);
		float level = isLocal ? 1f : 0.8f;
		// Guarded because GameChangeEventAfter can reach this before GameFlow exists on a peer.
		if (GameFlow.Instance != null && GameFlow.Instance.CurrentFaction != Faction) level -= 0.15f;
		Modulate = new Color(level, level, level, 1f);

		bool active = IsActive;
		_cloth.SetShaderParameter("active", active ? 1f : 0f);
		_cloth.SetShaderParameter("sway", active ? 4.5f : 1.2f);
		_cloth.SetShaderParameter("speed", active ? 2.6f : 1.2f);
	}

	/// <summary>
	/// The score on the banner and the banner's length. A change lets the cloth drop to its new length
	/// with a little bounce, punches the number, and floats the gain off the hem.
	/// </summary>
	private void RefreshScore()
	{
		int score = CombinedScore;
		if (score == _shownScore) return;
		int gained = _shownScore == int.MinValue ? 0 : score - _shownScore;
		_shownScore = score;
		ScoreLabel.Text = score.ToString();

		float target = Mathf.Clamp(MinTail + score * TailPerPoint, MinTail, MaxTail);
		_tailTween?.Kill();
		if (gained == 0 || !IsInsideTree())
		{
			Tail = target;
			return;
		}

		_tailTween = CreateTween();
		_tailTween.TweenProperty(this, PropertyName.Tail.ToString(), target, 0.9f)
			.SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);

		Tween punch = CreateTween();
		punch.TweenProperty(ScoreLabel.LabelSettings, "font_size", 46, GameSettings.DurationShortSeconds);
		punch.TweenProperty(ScoreLabel.LabelSettings, "font_size", 32, GameSettings.DurationShortSeconds);

		FloatGain(gained, ClothTop + _flagHeight + target);
	}

	/// <summary>A gold "+N" that rises from the hem and fades.</summary>
	private void FloatGain(int gained, float hemY)
	{
		Label floater = new()
		{
			Text = gained > 0 ? $"+{gained}" : gained.ToString(),
			LabelSettings = new LabelSettings { FontSize = 22, FontColor = new Color(0.961f, 0.851f, 0.541f), OutlineSize = 6, OutlineColor = Colors.Black },
			HorizontalAlignment = HorizontalAlignment.Center,
			Size = new Vector2(Cloth.Size.X, 28),
			Position = new Vector2(0, hemY),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		AddChild(floater);
		Tween t = floater.CreateTween().SetParallel();
		t.TweenProperty(floater, "position:y", hemY - 40, 1.6f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		t.TweenProperty(floater, "modulate:a", 0f, 1.6f).SetEase(Tween.EaseType.In);
		t.Chain().TweenCallback(Callable.From(floater.QueueFree));
	}

	/// <summary>Repaints the pool counters: how many pieces of each type the faction has left to play.</summary>
	private void SetUnitCounts()
	{
		SetUnitCount(ArmyCountLabel, UnitPool.AvailableUnitCount(Faction, UnitType.ARMY), TagInk);
		SetUnitCount(NavyCountLabel, UnitPool.AvailableUnitCount(Faction, UnitType.NAVY), TagInk);
		foreach (((Faction faction, UnitType type), (Label label, Color ink)) in _builtCountLabels)
			SetUnitCount(label, UnitPool.AvailableUnitCount(faction, type), ink);
	}

	/// <summary>
	/// Repaints the hand, draw-deck and table counters. Reads state directly rather than tracking
	/// deltas: several paths change the piles without emitting CardsDrawn/CardsDiscarded.
	/// </summary>
	private void SetCardCounts()
	{
		// FactionState.ForEnum returns null before the game state exists — same guard as UnitPool.AvailableUnitCount.
		DeckState deckState = FactionState?.DeckState;
		HandButton.Text = (deckState?.HandCardIds.Count ?? 0).ToString();
		DeckButton.Text = (deckState?.DeckCardIds.Count ?? 0).ToString();
		// The same two piles the Active Cards modal lists, so the counter can never disagree with it.
		int activeCardCount = (deckState?.StatusCardIds.Count ?? 0) + (deckState?.ResponseCardIds.Count ?? 0);
		TableButton.Text = activeCardCount.ToString();
	}

	/// <summary>Red when the pool is empty, amber at one, else the tag's own ink.</summary>
	private static void SetUnitCount(Label label, int count, Color ink)
	{
		label.Text = count.ToString();
		// FontColor on the LabelSettings: a Label with label_settings ignores theme colour overrides.
		label.LabelSettings.FontColor = count switch
		{
			0 => new Color(0.75f, 0.1f, 0.1f),
			1 => new Color(0.7f, 0.33f, 0f),
			_ => ink,
		};
	}

	// ── Extra tags: Air Forces and minor factions ────────────────────────────

	/// <summary>Counters built in code rather than in the scene, with the ink each one rests at.</summary>
	private readonly Dictionary<(Faction, UnitType), (Label, Color)> _builtCountLabels = new();

	private static bool HasAirForces(Faction faction) => StaticGameData.FactionDataMap[faction].NumberOfAirUnits > 0;

	private List<Faction> _minors = new();

	/// <summary>This row's faction, or a minor faction it acts for.</summary>
	private bool IsShownHere(Faction faction) => faction == Faction || _minors.Contains(faction);

	/// <summary>The row's headline score: this faction's points plus those of the minor factions it acts for.</summary>
	private int CombinedScore => VPHistory.CombinedScore(Faction);

	/// <summary>A minor faction's pieces in reserve hang as tags in its own colour under the row's own.</summary>
	private void AddMinorTags()
	{
		foreach (Faction minor in _minors)
		{
			FactionData data = StaticGameData.FactionDataMap[minor];
			AddCounterTag(minor, UnitType.ARMY, data.FactionColor);
			if (data.NumberOfNavyUnits > 0) AddCounterTag(minor, UnitType.NAVY, data.FactionColor);
			if (data.NumberOfAirUnits > 0) AddCounterTag(minor, UnitType.AIR_FORCE, data.FactionColor);
		}
	}

	/// <summary>
	/// A tag for <paramref name="faction"/>'s pool of <paramref name="type"/>, copied from the army tag.
	/// <paramref name="plate"/> recolours the tag (a minor faction's); null keeps the brass.
	/// </summary>
	private void AddCounterTag(Faction faction, UnitType type, Color? plate)
	{
		PanelContainer tag = (PanelContainer)ArmyTag.Duplicate();
		tag.UniqueNameInOwner = false;
		FactionData data = StaticGameData.FactionDataMap[faction];
		string pieces = type switch { UnitType.NAVY => "navies", UnitType.AIR_FORCE => "air forces", _ => "armies" };
		tag.TooltipText = $"{data.Label}: {pieces} in reserve";
		Tags.AddChild(tag);

		TextureRect icon = tag.GetNode<TextureRect>("Cell/ArmyIcon");
		icon.UniqueNameInOwner = false;
		icon.Texture = type switch
		{
			UnitType.NAVY => NavyIcon.Texture,
			UnitType.AIR_FORCE => AssetRepository.AirForceSprite,
			_ => ArmyIcon.Texture,
		};
		icon.SelfModulate = plate.HasValue ? Colors.White : data.FactionColor;

		Label label = tag.GetNode<Label>("Cell/ArmyCountLabel");
		label.UniqueNameInOwner = false;
		label.LabelSettings = (LabelSettings)ArmyCountLabel.LabelSettings.Duplicate();
		Color ink = TagInk;
		if (plate.HasValue)
		{
			StyleBoxFlat style = (StyleBoxFlat)tag.GetThemeStylebox("panel").Duplicate();
			style.BgColor = plate.Value;
			tag.AddThemeStyleboxOverride("panel", style);
			ink = Colors.White;
			label.LabelSettings.OutlineSize = 4;
			label.LabelSettings.OutlineColor = Colors.Black;
		}
		_builtCountLabels[(faction, type)] = (label, ink);
	}

	public void ShowCardDelta(int delta)
	{
		CardsAnimationControl.ShowCardsAnimation(delta);
	}

	/// <summary>Where the Active Cards button sits on screen, for a card shown going onto the table.</summary>
	public Rect2 ActiveCardsButtonRect => TableButton.GetGlobalRect();
}
