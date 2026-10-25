using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The turn's steps in a row under the faction bar, the one under way in the turn faction's colour,
/// a hint box under the step the mouse is over, and an InputBucket per faction being asked at its end.
/// Read off GameFlow's step table, so an expansion's steps show up without this naming them.
/// </summary>
public partial class TurnSequenceStrip : Control
{
	private HBoxContainer Steps => GetNode<HBoxContainer>("%Steps");
	private Control Bucket => GetNode<Control>("%Bucket");
	private Control Hint => GetNode<Control>("%Hint");
	private Label HintTitle => GetNode<Label>("%HintTitle");
	private Label HintRule => GetNode<Label>("%HintRule");

	private static readonly Color PillColor = new(0.1f, 0.1f, 0.1f, 0.6f);
	private static readonly Color CurrentBorderColor = new(1f, 1f, 1f, 0.9f);
	private const float DoneAlpha = 0.4f;
	private const float HintGap = 4f;
	private const float HintMaxWidth = 320f;
	private const float BucketGap = 8f;
	private const float BucketRise = 3f;

	private readonly List<TurnStep> _order = new();
	private readonly List<PanelContainer> _pills = new();

	private bool _subscribed;
	private int _turn;
	private TurnStep? _leftoverStep;
	private (int Index, Faction Faction)? _painted;
	private int _hinted = -1;

	public override void _Ready()
	{
		Visible = false;
		// Both: the steps re-centre when the strip first gets its width, without resizing themselves.
		Steps.Resized += PlaceBucket;
		Resized += PlaceBucket;

		// One HUD per PlayerScene; only the local copy is ever on screen.
		if (!SessionIdentity.IsLocalAuthority(this)) return;
		EventBus.Instance.GameChangeEventAfter += OnGameChangeEventAfter;
		EventBus.Instance.NextStepStarted += OnNextStepStarted;
		_subscribed = true;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (!_subscribed || EventBus.Instance == null) return;
		_subscribed = false;
		EventBus.Instance.GameChangeEventAfter -= OnGameChangeEventAfter;
		EventBus.Instance.NextStepStarted -= OnNextStepStarted;
	}

	// The same pair as RoundTrack, for the same reason: both fire on clients and during a save replay.
	private void OnGameChangeEventAfter(string changeEventName) => Refresh();

	private void OnNextStepStarted(int turnStep) => Refresh();

	private void Refresh()
	{
		GameFlow flow = GameFlow.Instance;
		Visible = flow != null && flow.GameTurn > 0;
		if (!Visible) return;

		if (_order.Count == 0) Build();
		int current = CurrentIndex(flow);
		Faction faction = flow.CurrentFaction;
		if (_painted == (current, faction)) return;

		_painted = (current, faction);
		Paint(current, faction);
	}

	/// <summary>
	/// Index of the step under way, or -1 before the turn's first one. A turn that has just rolled over
	/// still reads the previous faction's last step until its START lands, which would show every step
	/// done; a rollover seen live is held at -1 until the step moves. One seen on the first refresh
	/// (a rejoin or a restore) is trusted as it stands.
	/// </summary>
	private int CurrentIndex(GameFlow flow)
	{
		if (flow.GameTurn != _turn)
		{
			_leftoverStep = _turn > 0 ? flow.TurnStep : null;
			_turn = flow.GameTurn;
		}
		if (_leftoverStep != flow.TurnStep) _leftoverStep = null;
		return _leftoverStep == null ? _order.IndexOf(flow.TurnStep) : -1;
	}

	private void Build()
	{
		_order.AddRange(GameFlow.Instance.StepOrder.Where(step => step != TurnStep.START && step != TurnStep.END));

		for (int i = 0; i < _order.Count; i++)
		{
			int index = i;
			PanelContainer pill = new() { MouseFilter = MouseFilterEnum.Stop };
			pill.MouseEntered += () => ShowHint(index);
			pill.MouseExited += () => HideHint(index);
			pill.AddChild(new Label
			{
				Text = $"{i + 1}  {TurnStepText.For(_order[i]).Name}",
				MouseFilter = MouseFilterEnum.Ignore,
				LabelSettings = new LabelSettings { FontSize = 15, OutlineSize = 6, OutlineColor = Colors.Black },
			});
			Steps.AddChild(pill);
			_pills.Add(pill);
		}

		CustomMinimumSize = new Vector2(0, Steps.GetCombinedMinimumSize().Y);
	}

	private void Paint(int current, Faction faction)
	{
		Color factionColor = StaticGameData.FactionDataMap.TryGetValue(faction, out FactionData data)
			? data.FactionColor : PillColor;

		for (int i = 0; i < _order.Count; i++)
		{
			StyleBoxFlat pill = i == current ? PillStyle(factionColor, CurrentBorderColor, 2)
				: PillStyle(PillColor, Colors.Transparent, 0);
			_pills[i].AddThemeStyleboxOverride("panel", pill);
			_pills[i].Modulate = new Color(1, 1, 1, i < current ? DoneAlpha : 1);
		}
	}

	/// <summary>Hung off the strip's right end; the buckets stand a little taller than the pills, so they rise to centre on them.</summary>
	private void PlaceBucket()
		=> Bucket.Position = new Vector2(Steps.Position.X + Steps.Size.X + BucketGap, -BucketRise);

	private static StyleBoxFlat PillStyle(Color fill, Color border, int borderWidth)
	{
		StyleBoxFlat style = new()
		{
			BgColor = fill,
			BorderColor = border,
			ContentMarginLeft = 9, ContentMarginRight = 9, ContentMarginTop = 2, ContentMarginBottom = 3,
		};
		style.SetCornerRadiusAll(5);
		style.SetBorderWidthAll(borderWidth);
		return style;
	}

	private void ShowHint(int index)
	{
		TurnStepText text = TurnStepText.For(_order[index]);
		HintTitle.Text = text.Optional ? $"{text.Name} Step (optional)" : $"{text.Name} Step";
		HintRule.Text = WrapToWidth(text.Rule, HintRule.GetThemeFont("font"), HintRule.LabelSettings.FontSize, HintMaxWidth);

		_hinted = index;
		Hint.Visible = true;
		Hint.ResetSize();
		Control pill = _pills[index];
		float centre = Steps.Position.X + pill.Position.X + pill.Size.X / 2;
		Hint.Position = new Vector2(centre - Hint.Size.X / 2, Steps.Size.Y + HintGap);
	}

	/// <summary>
	/// Breaks the rule into lines by hand rather than autowrapping the label: an autowrapped label sizes
	/// its height from the width it had at the last layout, which left the box tall or with blank lines.
	/// </summary>
	private static string WrapToWidth(string text, Font font, int fontSize, float maxWidth)
	{
		List<string> lines = new();
		string line = "";
		foreach (string word in text.Split(' '))
		{
			string longer = line.Length == 0 ? word : $"{line} {word}";
			if (line.Length > 0 && font.GetStringSize(longer, HorizontalAlignment.Left, -1, fontSize).X > maxWidth)
			{
				lines.Add(line);
				line = word;
			}
			else line = longer;
		}
		lines.Add(line);
		return string.Join("\n", lines);
	}

	/// <summary>Only the step that opened the hint closes it, in case the next pill's enter lands first.</summary>
	private void HideHint(int index)
	{
		if (_hinted != index) return;
		_hinted = -1;
		Hint.Visible = false;
	}
}
