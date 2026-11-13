using Godot;

/// <summary>
/// A "Round N of M" pill under the turn-step strip, styled like its pills. On the last round it reads "Last round N"
/// in amber, and the plate border turns amber too.
///
/// Refreshed off the same pair as TurnFactionTint, for the same reason: both fire on every peer and
/// during a save replay, where the host-only NewTurnStarted would leave a client on round 1.
/// </summary>
public partial class RoundTrack : PanelContainer
{
	private Label Heading => GetNode<Label>("%Heading");

	private static readonly Color AmberColor = new("#fac775");
	private static readonly Color AmberBorderColor = new("#ef9f27");
	private static readonly Color BrassEdgeColor = new(0.851f, 0.678f, 0.361f, 0.28f);
	private static readonly Color CreamColor = new(0.929f, 0.898f, 0.82f);

	private StyleBoxFlat _plate;
	private (int Round, int Max)? _painted;

	public override void _Ready()
	{
		// One HUD per PlayerScene, so the plate and label settings are duplicated before the last round tints them.
		_plate = (StyleBoxFlat)GetThemeStylebox("panel").Duplicate();
		AddThemeStyleboxOverride("panel", _plate);
		Heading.LabelSettings = (LabelSettings)Heading.LabelSettings.Duplicate();

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
		// Styled like a turn-step pill; the last round borrows the current step's 2px border, in amber.
		bool last = round >= max;
		_plate.BorderColor = last ? AmberBorderColor : BrassEdgeColor;
		_plate.SetBorderWidthAll(last ? 2 : 1);
		Heading.LabelSettings.FontColor = last ? AmberColor : CreamColor;
		Heading.Text = (last ? $"Last round {round}" : $"Round {round} of {max}").ToUpperInvariant();
	}
}
