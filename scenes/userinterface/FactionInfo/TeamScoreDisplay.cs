using Godot;

/// <summary>
/// The Axis / Allies running total on the flag flanking the faction strip — the sum of the three
/// per-faction scores <see cref="FactionInfoRow"/> shows individually, and the number GameFlow's
/// 30-point lead check actually decides the game on.
///
/// Deliberately not driven by <see cref="FactionsContainer"/>: that node only wires itself up on the
/// multiplayer authority peer, and these totals have to be right on every peer, so this manages its
/// own subscriptions the way FactionInfoRow does.
/// </summary>
public partial class TeamScoreDisplay : TextureRect
{
	[Export] public FactionTeam Team { get; set; } = FactionTeam.NONE;

	private Label ScoreLabel => GetNode<Label>("ScoreLabel");

	public override void _Ready()
	{
		// The punch tween animates font_size on the resource itself, so the Axis and Allies labels
		// must not share one instance — same reason FactionInfoRow duplicates its LabelSettings.
		ScoreLabel.LabelSettings = (LabelSettings)ScoreLabel.LabelSettings.Duplicate();

		// Read the current total before subscribing: two write paths change FactionState.Score without
		// emitting FactionScoredPoints — SetStartingScoreChangeEvent (scenario starting VP) and
		// MultiplayerGameState.ApplySnapshot (a client receiving state) — so the event stream alone
		// would leave a joining or mid-scenario peer showing zero.
		Refresh();

		EventBus.Instance.FactionScoredPoints += OnFactionScoredPoints;
		EventBus.Instance.GameStateRecalculated += Refresh;
		EventBus.Instance.NewTurnStarted += OnNewTurnStarted;
	}

	public override void _ExitTree()
	{
		if (EventBus.Instance != null)
		{
			EventBus.Instance.FactionScoredPoints -= OnFactionScoredPoints;
			EventBus.Instance.GameStateRecalculated -= Refresh;
			EventBus.Instance.NewTurnStarted -= OnNewTurnStarted;
		}
	}

	/// <summary>
	/// The payload's newScore is one faction's score, not the team's, so the total is always recomputed
	/// from state rather than accumulated from the event.
	/// </summary>
	private void OnFactionScoredPoints(Faction faction, int newScore)
	{
		if (StaticGameData.FactionTeamForFaction(faction) == Team)
		{
			SetScore(StaticGameData.ScoreForTeam(Team), punch: true);
		}
	}

	private void OnNewTurnStarted(int turnNumber)
	{
		Refresh();
	}

	/// <summary>Repaints from state without animating — for the signal-less write paths.</summary>
	private void Refresh()
	{
		SetScore(StaticGameData.ScoreForTeam(Team), punch: false);
	}

	private void SetScore(int score, bool punch)
	{
		ScoreLabel.Text = score.ToString();
		if (!punch) return;

		Tween tween = GetTree().CreateTween();
		tween.TweenProperty(ScoreLabel.LabelSettings, "font_size", 84, GameSettings.DurationShortSeconds);
		tween.TweenProperty(ScoreLabel.LabelSettings, "font_size", 48, GameSettings.DurationShortSeconds);
	}
}
