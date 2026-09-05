using Godot;

/// <summary>
/// The "Round N" readout in the top-right corner. Reads <see cref="GameFlow.Round"/>, which is derived
/// from GameTurn rather than stored, so there is nothing to keep in sync — only a repaint to trigger.
///
/// Deliberately NOT driven by EventBus.NewTurnStarted: that is emitted from GameFlow.StartNewTurn,
/// which only the host runs, so a client would sit on "Round 1" forever and a save restore (which
/// replays events without running the turn loop) would never repaint either. GameChangeEventAfter
/// fires on every peer after every applied ChangeEvent — ChangeRoundChangeEvent included — and so does
/// a replay, which covers both. NextStepStarted comes off the replicated TurnStepCounter setter and
/// catches the post-restore resume, where the round has already moved before any new event lands.
/// </summary>
public partial class GameRoundLabel : Label
{
	public override void _Ready()
	{
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
	/// Repaints from state. Runs on every ChangeEvent, so it leaves the text alone when the round has
	/// not moved — and leaves whatever the scene ships with alone before a game exists.
	/// </summary>
	private void Refresh()
	{
		if (GameFlow.Instance == null) return;

		string text = $"Round {GameFlow.Instance.Round}";
		if (Text != text) Text = text;
	}
}
