using Godot;

/// <summary>
/// "Round N of M" over a track of one segment per round, under the Axis / Allies totals: the round
/// limit beside the 30-point lead, the two things the game ends on. On the last round the plate
/// turns amber.
///
/// Refreshed off the same pair as TurnFactionTint, for the same reason: both fire on every peer and
/// during a save replay, where the host-only NewTurnStarted would leave a client on round 1.
/// </summary>
public partial class RoundTrack : PanelContainer
{
	private RichTextLabel Heading => GetNode<RichTextLabel>("%Heading");
	private HBoxContainer Segments => GetNode<HBoxContainer>("%Segments");

	private const int NumberFontSize = 20;

	private static readonly Color CaptionColor = new(0.74f, 0.74f, 0.74f);
	private static readonly Color LimitColor = new(0.56f, 0.56f, 0.56f);
	private static readonly Color DoneColor = new(0.86f, 0.86f, 0.86f, 0.55f);
	private static readonly Color FutureColor = new(1f, 1f, 1f, 0.14f);
	private static readonly Color AmberColor = new("#fac775");
	private static readonly Color AmberBorderColor = new("#ef9f27");

	private StyleBoxFlat _plate;
	private Color _borderColor;
	private (int Round, int Max)? _painted;

	public override void _Ready()
	{
		// One HUD per PlayerScene, so the plate is duplicated before the last round tints its border.
		_plate = (StyleBoxFlat)GetThemeStylebox("panel").Duplicate();
		AddThemeStyleboxOverride("panel", _plate);
		_borderColor = _plate.BorderColor;

		Refresh();

		EventBus.Instance.GameChangeEventAfter += OnGameChangeEventAfter;
		EventBus.Instance.NextStepStarted += OnNextStepStarted;
	}

	public override void _ExitTree()
	{
		if (EventBus.Instance != null)
		{
			EventBus.Instance.GameChangeEventAfter -= OnGameChangeEventAfter;
			EventBus.Instance.NextStepStarted -= OnNextStepStarted;
		}
	}

	private void OnGameChangeEventAfter(string changeEventName) => Refresh();

	private void OnNextStepStarted(int turnStep) => Refresh();

	/// <summary>
	/// Repaints from state when the round or the limit has moved. The limit is watched too: on a client
	/// it arrives through the synchronizer, which raises no event of its own.
	/// </summary>
	private void Refresh()
	{
		GameFlow flow = GameFlow.Instance;
		if (flow == null || flow.MaxRound < 1) return;

		(int, int) state = (flow.Round, flow.MaxRound);
		if (_painted == state) return;

		_painted = state;
		Paint(flow.Round, flow.MaxRound);
	}

	private void Paint(int round, int max)
	{
		bool last = round >= max;
		_plate.BorderColor = last ? AmberBorderColor : _borderColor;
		string number = $"[font_size={NumberFontSize}]{round}[/font_size]";
		Heading.Text = last
			? $"[center][color=#{AmberColor.ToHtml(false)}]Last round {number}[/color][/center]"
			: $"[center][color=#{CaptionColor.ToHtml(false)}]Round[/color] {number} [color=#{LimitColor.ToHtml(false)}]of {max}[/color][/center]";

		if (Segments.GetChildCount() != max) BuildSegments(max);
		for (int r = 1; r <= max; r++)
			Segments.GetChild<ColorRect>(r - 1).Color = r < round ? DoneColor : r == round ? Colors.White : FutureColor;
	}

	private void BuildSegments(int count)
	{
		// RemoveChild before QueueFree: a queued child still counts in GetChildCount this frame.
		foreach (Node child in Segments.GetChildren())
		{
			Segments.RemoveChild(child);
			child.QueueFree();
		}

		for (int i = 0; i < count; i++)
		{
			Segments.AddChild(new ColorRect
			{
				CustomMinimumSize = new Vector2(0, 5),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				MouseFilter = MouseFilterEnum.Ignore,
			});
		}
	}
}
