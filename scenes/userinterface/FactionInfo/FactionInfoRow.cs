using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// One faction in the flagpole column on the left: a pennant with the flag at the hoist and the score
/// on it, and a tail in the faction's colour that flies further out the more points the faction has,
/// so the column itself is the score race. Cards in hand, deck and on the table, and pieces in reserve,
/// are printed along the tail. The faction whose turn it is flies hardest. Click for score history.
/// </summary>
public partial class FactionInfoRow : Control
{
	public Faction Faction;
	private FactionState FactionState => FactionState.ForEnum(Faction);

	private static readonly Shader ClothShader = GD.Load<Shader>("res://scenes/userinterface/FactionInfo/PennantCloth.gdshader");

	private ColorRect Cloth => GetNode<ColorRect>("%Cloth");
	private Label ScoreLabel => GetNode<Label>("%ScoreLabel");
	private Button FactionInfoButton => GetNode<Button>("%FactionInfoButton");
	private VBoxContainer Stats => GetNode<VBoxContainer>("%Stats");
	private HBoxContainer Pieces => GetNode<HBoxContainer>("%Pieces");
	private Button HandButton => GetNode<Button>("%HandButton");
	private Button DeckButton => GetNode<Button>("%DeckButton");
	private Button TableButton => GetNode<Button>("%TableButton");
	private HBoxContainer ArmyCell => GetNode<HBoxContainer>("%ArmyCell");
	private TextureRect ArmyIcon => GetNode<TextureRect>("%ArmyIcon");
	private TextureRect NavyIcon => GetNode<TextureRect>("%NavyIcon");
	private Label ArmyCountLabel => GetNode<Label>("%ArmyCountLabel");
	private Label NavyCountLabel => GetNode<Label>("%NavyCountLabel");
	private CardsAnimationControl CardsAnimationControl => GetNode<CardsAnimationControl>("CardsAnimationControl");

	// ── Pennant geometry ─────────────────────────────────────────────────────

	/// <summary>Width of the flag at the hoist: the pennant's height at the flag's own aspect, so the whole flag shows.</summary>
	private float Head = 104f;
	/// <summary>Tail at zero points; it is never shorter than the counts printed on it need.</summary>
	private const float MinTail = 130f;
	/// <summary>Extra tail per point, and the most it adds, so a runaway leader never spans the map.</summary>
	private const float TailPerPoint = 3f;
	private const float MaxExtra = 150f;
	private const float Notch = 18f;
	/// <summary>The pennant's height at rest, and the headroom its cloth gets for the ripple.</summary>
	private const float Height = 52f;
	private const float RippleMargin = 8f;

	private ShaderMaterial _cloth;
	private float _minTail = MinTail;
	private float _tail = MinTail;
	private Tween _tailTween;
	private int _shownScore = int.MinValue;
	private Color _ink = Colors.White;

	/// <summary>Length of the tail after the flag; setting it re-lays the cloth and the click area.</summary>
	public float Tail
	{
		get => _tail;
		set
		{
			_tail = value;
			Vector2 size = new(Head + _tail, Height);
			// Drawn into a rect with headroom on the free sides, so the ripple is never clipped.
			Cloth.Position = new Vector2(0, -RippleMargin);
			Cloth.Size = size + new Vector2(RippleMargin, 2 * RippleMargin);
			_cloth.SetShaderParameter("size", size);
			FactionInfoButton.Size = size;
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

		// One material per row: every pennant has its own flag, length and sway.
		Texture2D flag = data.FlagTexture;
		float aspect = (float)flag.GetWidth() / flag.GetHeight();
		Head = Mathf.Round(Height * aspect);
		ScoreLabel.Size = new Vector2(Head, Height);
		Stats.Position = Stats.Position with { X = Head + 10f };
		_cloth = new ShaderMaterial { Shader = ClothShader };
		_cloth.SetShaderParameter("flag", flag);
		_cloth.SetShaderParameter("tail_color", factionColor);
		_cloth.SetShaderParameter("head", Head);
		_cloth.SetShaderParameter("notch", Notch);
		_cloth.SetShaderParameter("margin", RippleMargin);
		_cloth.SetShaderParameter("phase", GD.Randf() * 10f);
		Cloth.Material = _cloth;

		ScoreLabel.LabelSettings = (LabelSettings)ScoreLabel.LabelSettings.Duplicate();

		FactionInfoButton.TooltipText = $"{data.FactionAdjactiveLabel} score history";
		FactionInfoButton.MouseEntered += () => _cloth.SetShaderParameter("hover", 1f);
		FactionInfoButton.MouseExited += () => _cloth.SetShaderParameter("hover", 0f);
		HandButton.TooltipText = "Cards in hand";
		DeckButton.TooltipText = "Cards left in the draw deck\nClick to see the discard pile";
		TableButton.TooltipText = "Status and response cards on the table\nClick to see them";
		ArmyCell.TooltipText = "Armies in reserve";
		GetNode<Control>("%NavyCell").TooltipText = "Navies in reserve";

		// Each count label recolours independently, so neither may share a LabelSettings instance.
		ArmyCountLabel.LabelSettings = (LabelSettings)ArmyCountLabel.LabelSettings.Duplicate();
		NavyCountLabel.LabelSettings = (LabelSettings)NavyCountLabel.LabelSettings.Duplicate();

		// The minor factions this faction acts for share its pennant: their points fold into its score
		// and their pieces in reserve are printed after its own, in their own colour.
		_minors = StaticGameData.MinorsOf(Faction);
		if (HasAirForces(Faction)) AddCounter(Faction, UnitType.AIR_FORCE, null);
		foreach (Faction minor in _minors)
		{
			FactionData minorData = StaticGameData.FactionDataMap[minor];
			AddCounter(minor, UnitType.ARMY, minorData.FactionColor);
			if (minorData.NumberOfNavyUnits > 0) AddCounter(minor, UnitType.NAVY, minorData.FactionColor);
			if (minorData.NumberOfAirUnits > 0) AddCounter(minor, UnitType.AIR_FORCE, minorData.FactionColor);
		}

		ApplyInk(factionColor);
		// A row whose minor factions print more than the base tail holds gets a longer baseline.
		_minTail = Mathf.Max(MinTail, Stats.Position.X - Head + Stats.GetCombinedMinimumSize().X + Notch + 10f);
		Tail = _minTail;

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

	/// <summary>
	/// Counts are printed straight onto the tail: white with a black edge on a dark tail, near-black on a
	/// pale one (Japan's), so they read without a plate behind them. Unit icons take the same ink.
	/// </summary>
	private void ApplyInk(Color tail)
	{
		bool pale = tail.Luminance > 0.55f;
		_ink = pale ? new Color(0.1f, 0.09f, 0.08f) : Colors.White;
		Color edge = pale ? new Color(1, 1, 1, 0.6f) : Colors.Black;
		Color hover = pale ? new Color(0.45f, 0.3f, 0.05f) : new Color(0.961f, 0.851f, 0.541f);

		foreach (Button button in new[] { HandButton, DeckButton, TableButton })
		{
			button.AddThemeColorOverride("font_color", _ink);
			button.AddThemeColorOverride("font_pressed_color", _ink);
			button.AddThemeColorOverride("font_hover_color", button == HandButton ? _ink : hover);
			button.AddThemeColorOverride("icon_normal_color", _ink == Colors.White ? Colors.White : _ink);
			button.AddThemeColorOverride("icon_hover_color", button == HandButton ? _ink : hover);
			button.AddThemeColorOverride("icon_pressed_color", hover);
			button.AddThemeColorOverride("font_outline_color", edge);
		}
		ArmyIcon.SelfModulate = _ink;
		NavyIcon.SelfModulate = _ink;
		foreach ((TextureRect icon, Label label, Faction faction, UnitType type) in _untinted)
		{
			icon.SelfModulate = _ink;
			_builtCountLabels[(faction, type)] = (label, _ink);
		}
		foreach (Label label in _builtCountLabels.Values.Select(v => v.Item1).Append(ArmyCountLabel).Append(NavyCountLabel))
			label.LabelSettings.OutlineColor = edge;
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
	/// Local factions at full strength, others a little dimmed, more so off their turn. The active
	/// faction's pennant flies hard and its trim glints; the rest barely stir.
	/// </summary>
	private void SetModulation()
	{
		// Gentle: the active pennant already stands out by flying, and dimming cloth much further
		// turns the whole column to mud.
		bool isLocal = PlayerFactionRegistry.GetLocalPlayerFactions().Contains(Faction);
		float level = isLocal ? 1f : 0.8f;
		// Guarded because GameChangeEventAfter can reach this before GameFlow exists on a peer.
		if (GameFlow.Instance != null && GameFlow.Instance.CurrentFaction != Faction) level -= 0.15f;
		Modulate = new Color(level, level, level, 1f);

		bool active = IsActive;
		_cloth.SetShaderParameter("active", active ? 1f : 0f);
		_cloth.SetShaderParameter("sway", active ? 4f : 1.2f);
		_cloth.SetShaderParameter("speed", active ? 2.6f : 1.2f);
	}

	/// <summary>
	/// The score on the flag and the pennant's length. A change lets the tail fly out to its new length
	/// with a little overshoot, punches the number, and floats the gain off the fly end.
	/// </summary>
	private void RefreshScore()
	{
		int score = CombinedScore;
		if (score == _shownScore) return;
		int gained = _shownScore == int.MinValue ? 0 : score - _shownScore;
		_shownScore = score;
		ScoreLabel.Text = score.ToString();

		float target = _minTail + Mathf.Clamp(score * TailPerPoint, 0f, MaxExtra);
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

		FloatGain(gained, Head + target);
	}

	/// <summary>A gold "+N" that drifts off the fly end and fades.</summary>
	private void FloatGain(int gained, float flyX)
	{
		Label floater = new()
		{
			Text = gained > 0 ? $"+{gained}" : gained.ToString(),
			LabelSettings = new LabelSettings { FontSize = 24, FontColor = new Color(0.961f, 0.851f, 0.541f), OutlineSize = 6, OutlineColor = Colors.Black },
			VerticalAlignment = VerticalAlignment.Center,
			Size = new Vector2(60, Height),
			Position = new Vector2(flyX + 4, 0),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		AddChild(floater);
		Tween t = floater.CreateTween().SetParallel();
		t.TweenProperty(floater, "position:x", flyX + 44, 1.6f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		t.TweenProperty(floater, "modulate:a", 0f, 1.6f).SetEase(Tween.EaseType.In);
		t.Chain().TweenCallback(Callable.From(floater.QueueFree));
	}

	/// <summary>Repaints the pool counters: how many pieces of each type the faction has left to play.</summary>
	private void SetUnitCounts()
	{
		SetUnitCount(ArmyCountLabel, UnitPool.AvailableUnitCount(Faction, UnitType.ARMY), _ink);
		SetUnitCount(NavyCountLabel, UnitPool.AvailableUnitCount(Faction, UnitType.NAVY), _ink);
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

	/// <summary>Red when the pool is empty, amber at one, else the counter's own ink.</summary>
	private static void SetUnitCount(Label label, int count, Color ink)
	{
		label.Text = count.ToString();
		// FontColor on the LabelSettings: a Label with label_settings ignores theme colour overrides.
		label.LabelSettings.FontColor = count switch
		{
			0 => new Color(1f, 0.3f, 0.25f),
			1 => new Color(1f, 0.65f, 0.15f),
			_ => ink,
		};
	}

	// ── Extra counters: Air Forces and minor factions ────────────────────────

	/// <summary>Counters built in code rather than in the scene, with the ink each one rests at.</summary>
	private readonly Dictionary<(Faction, UnitType), (Label, Color)> _builtCountLabels = new();

	private static bool HasAirForces(Faction faction) => StaticGameData.FactionDataMap[faction].NumberOfAirUnits > 0;

	private List<Faction> _minors = new();

	/// <summary>This row's faction, or a minor faction it acts for.</summary>
	private bool IsShownHere(Faction faction) => faction == Faction || _minors.Contains(faction);

	/// <summary>The row's headline score: this faction's points plus those of the minor factions it acts for.</summary>
	private int CombinedScore => VPHistory.CombinedScore(Faction);

	/// <summary>
	/// A counter for <paramref name="faction"/>'s pool of <paramref name="type"/>, copied from the army one
	/// and printed after it. <paramref name="tint"/> colours a minor faction's icon and number so it reads
	/// as theirs; null takes the pennant's own ink.
	/// </summary>
	private void AddCounter(Faction faction, UnitType type, Color? tint)
	{
		HBoxContainer cell = (HBoxContainer)ArmyCell.Duplicate();
		cell.UniqueNameInOwner = false;
		FactionData data = StaticGameData.FactionDataMap[faction];
		string pieces = type switch { UnitType.NAVY => "navies", UnitType.AIR_FORCE => "air forces", _ => "armies" };
		cell.TooltipText = $"{data.Label}: {pieces} in reserve";
		Pieces.AddChild(cell);

		TextureRect icon = cell.GetNode<TextureRect>("ArmyIcon");
		icon.UniqueNameInOwner = false;
		icon.Texture = type switch
		{
			UnitType.NAVY => NavyIcon.Texture,
			UnitType.AIR_FORCE => AssetRepository.AirForceSprite,
			_ => ArmyIcon.Texture,
		};

		Label label = cell.GetNode<Label>("ArmyCountLabel");
		label.UniqueNameInOwner = false;
		label.LabelSettings = (LabelSettings)ArmyCountLabel.LabelSettings.Duplicate();
		// An untinted counter takes the pennant's ink, which ApplyInk fills in once it is known.
		_builtCountLabels[(faction, type)] = (label, tint ?? Colors.White);
		if (tint.HasValue) icon.SelfModulate = tint.Value;
		else _untinted.Add((icon, label, faction, type));
	}

	/// <summary>Built counters that take the pennant's ink rather than a minor faction's tint.</summary>
	private readonly List<(TextureRect, Label, Faction, UnitType)> _untinted = new();

	public void ShowCardDelta(int delta)
	{
		CardsAnimationControl.ShowCardsAnimation(delta);
	}

	/// <summary>Where the Active Cards button sits on screen, for a card shown going onto the table.</summary>
	public Rect2 ActiveCardsButtonRect => TableButton.GetGlobalRect();
}
