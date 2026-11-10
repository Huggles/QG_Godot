using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Score history, opened from a faction row's flag: every row's running total by round on one chart,
/// a button per faction to pick whose line is highlighted, and that faction's breakdown by round.
/// Layout lives in ScoreHistoryDialog.tscn, an inherited scene of <see cref="MenuModal"/>'s shell.
/// </summary>
public partial class ScoreHistoryDialog : MenuModal
{
	private static readonly PackedScene Scene =
		GD.Load<PackedScene>("res://scenes/userinterface/FactionInfo/ScoreHistoryDialog.tscn");

	private static ScoreHistoryDialog _open;

	private ScoreChart Chart => GetNode<ScoreChart>("%Chart");
	private HBoxContainer FactionButtons => GetNode<HBoxContainer>("%FactionButtons");
	private RichTextLabel Breakdown => GetNode<RichTextLabel>("%Breakdown");
	private Button CloseButton => GetNode<Button>("%CloseButton");

	private Faction _selected;
	private readonly ButtonGroup _group = new();

	/// <summary>Opens the window on <paramref name="faction"/>, or switches the one already open to it.</summary>
	public static void Open(Node parent, Faction faction)
	{
		if (parent == null) return;
		if (IsInstanceValid(_open) && !_open.Resolved)
		{
			_open.Select(faction);
			return;
		}
		_open = Scene.Instantiate<ScoreHistoryDialog>();
		_open._selected = faction;
		parent.AddChild(_open);
	}

	public override void _Ready()
	{
		base._Ready();
		Guard.Try(ReadyInternal, "ScoreHistoryDialog._Ready");
	}

	private void ReadyInternal()
	{
		CloseButton.Pressed += Cancel;
		foreach (Faction faction in StaticGameData.PlayableFactions)
			FactionButtons.AddChild(MakeFactionButton(faction));
		Select(_selected);
	}

	public override void _ExitTree()
	{
		if (_open == this) _open = null;
	}

	protected override void Cancel()
	{
		if (Resolved) return;
		Resolved = true;
		QueueFree();
	}

	/// <summary>A flag-backed toggle showing the row's total; pressing it highlights that faction.</summary>
	private Button MakeFactionButton(Faction faction)
	{
		FactionData data = StaticGameData.FactionDataMap[faction];
		Button button = new()
		{
			ToggleMode = true,
			ButtonGroup = _group,
			FocusMode = Control.FocusModeEnum.None,
			CustomMinimumSize = new Vector2(104, 62),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			Text = VPHistory.CombinedScore(faction).ToString(),
			TooltipText = data.Label,
			ButtonPressed = faction == _selected,
		};
		button.SetMeta("faction", (int)faction);
		button.AddThemeFontSizeOverride("font_size", 28);
		button.AddThemeColorOverride("font_color", Colors.White);
		button.AddThemeColorOverride("font_hover_color", Colors.White);
		button.AddThemeColorOverride("font_pressed_color", Colors.White);
		button.AddThemeColorOverride("font_outline_color", Colors.Black);
		button.AddThemeConstantOverride("outline_size", 6);

		// The flag is the button face, dimmed unless selected; the selected one also gets a brass rim.
		foreach ((string state, float dim) in new[] { ("normal", 0.5f), ("hover", 0.8f), ("pressed", 1f), ("hover_pressed", 1f) })
			button.AddThemeStyleboxOverride(state, new StyleBoxTexture { Texture = data.FlagTexture, ModulateColor = new Color(dim, dim, dim) });
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

		StyleBoxFlat rimStyle = new() { BgColor = Colors.Transparent, BorderColor = new Color(0.851f, 0.678f, 0.361f) };
		rimStyle.SetBorderWidthAll(3);
		Panel rim = new() { Name = "Rim", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = faction == _selected };
		rim.AddThemeStyleboxOverride("panel", rimStyle);
		rim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		button.AddChild(rim);

		button.Toggled += on => { if (on) Select(faction); };
		return button;
	}

	private void Select(Faction faction)
	{
		_selected = faction;
		foreach (Button button in FactionButtons.GetChildren().OfType<Button>())
		{
			bool on = (Faction)(int)button.GetMeta("faction") == faction;
			button.SetPressedNoSignal(on);
			button.GetNode<Panel>("Rim").Visible = on;
		}
		Chart.Show(faction);
		Breakdown.Text = BreakdownText(faction);
	}

	/// <summary>The faction's points round by round, newest first, its minor factions' lines tagged with their name.</summary>
	private static string BreakdownText(Faction faction)
	{
		FactionData data = StaticGameData.FactionDataMap[faction];
		List<string> lines = new() { $"[font_size=22][color=#d9ad5c]{data.Label}[/color] — {VPHistory.CombinedScore(faction)} VP[/font_size]" };

		var rounds = VPHistory.Summaries(faction).GroupBy(s => s.Round).OrderByDescending(g => g.Key).ToList();
		if (rounds.Count == 0)
			lines.Add("[color=#bbbbbb]No points scored yet.[/color]");

		foreach (IGrouping<int, VPTurnSummary> round in rounds)
		{
			lines.Add($"\n[color=#d9ad5c]Round {round.Key}[/color]  [color=#bbbbbb]{round.Sum(s => s.TotalScore):+#;-#;0} VP[/color]");
			foreach (VPTurnSummary summary in round)
			{
				string whose = summary.Faction == faction ? string.Empty : $" ({summary.Faction.Label()})";
				foreach (VPEntry entry in summary.victoryPointEntries)
					lines.Add($"   {entry.Description}{whose}");
			}
		}

		int start = VPHistory.StartingPoints(faction);
		if (start != 0) lines.Add($"\n[color=#d9ad5c]Start[/color]  [color=#bbbbbb]{start} VP[/color]");
		return string.Join("\n", lines);
	}
}
