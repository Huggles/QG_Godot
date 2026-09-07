using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// The turn announcement: the incoming faction's flag on a badge in the middle of the screen, faded
/// in, held for a beat, and faded out again.
///
/// Driven by <see cref="ChangeRoundChangeEvent"/>'s AfterAnimations rather than by an EventBus
/// signal, so it takes a defined place in the animation stream instead of racing whatever else the
/// turn boundary set off — and so it cannot go missing separately from the turn change it announces.
/// It does not block that queue — the new turn's first step animates underneath it — which is also
/// why nothing here may take a click; see <see cref="IgnoreMouseRecursively"/>.
/// </summary>
public partial class TurnBadge : Control
{
	/// <summary>Set in _Ready, cleared in _ExitTree. Null between game scenes, and headless.</summary>
	public static TurnBadge Instance;

	private TextureRect Flag => GetNode<TextureRect>("%Flag");

	/// <summary>
	/// The caption inside the instanced ScalableLabel. Reached by path rather than by a unique name
	/// because % only resolves nodes owned by THIS scene, and the label belongs to ScalableLabel.tscn
	/// — only its root comes through as %TurnLabel.
	/// </summary>
	private Label Caption => GetNode<Label>("%TurnLabel/MarginContainer/Label");

	/// <summary>The fade currently running, kept so the next turn can cut it short.</summary>
	private Tween _fade;

	/// <summary>
	/// The turn already announced, so the catch-up below cannot announce it a second time. -1 until
	/// the first announcement, which is never turn -1.
	/// </summary>
	private int _announcedTurn = -1;

	public override void _Ready()
	{
		Instance = this;
		EventBus.Instance.GameSessionStarted += OnGameSessionStarted;

		// The badge overlays a game the player is still playing, so anything here that could take a
		// click would swallow one from the middle of the screen for as long as the badge is up. Set
		// in code rather than per node in the scene so a node added to it later cannot reintroduce
		// the problem.
		IgnoreMouseRecursively(this);

		Hide();
		Modulate = new Color(Modulate, 0f);
	}

	/// <summary>
	/// EventBus is a process-wide static, so a handler left connected outlives this scene — and one
	/// stale handler aborts the whole emission for every handler queued behind it.
	/// </summary>
	public override void _ExitTree()
	{
		if (EventBus.Instance != null) EventBus.Instance.GameSessionStarted -= OnGameSessionStarted;
		if (Instance == this) Instance = null;
	}

	/// <summary>
	/// Announce the opening turn if it has already happened by the time this badge exists.
	///
	/// GameFlow.StartGame is started fire-and-forget and only then does PlayerScene build the HUD, so
	/// the two race. Nothing in StartGame is guaranteed to yield — the opening draw loop skips its
	/// await when a scenario pre-dealt full hands, an empty opening discard completes synchronously,
	/// OnGameStarted is only awaited when a program is installed, and ChangeRoundChangeEvent's own
	/// ExecuteAsync awaits Task.CompletedTask — so on those configurations the first turn change lands
	/// before this node exists, Announce finds a null Instance, and the opening badge is simply lost.
	///
	/// This signal is emitted immediately after the HUD is built, which makes it the first moment the
	/// badge could have shown. The turn guard in <see cref="Run"/> is what keeps this from
	/// double-announcing when the race comes out the other way and the ChangeEvent already got there.
	/// </summary>
	private void OnGameSessionStarted()
	{
		// GameTurn > 0 rather than GameFlow's private GameStarted flag, and a better test anyway: it
		// is the turn change itself, which is the thing being announced. Still 0 when the HUD won the
		// race, in which case there is nothing to catch up and the ChangeEvent will do it shortly.
		if (GameFlow.Instance == null || GameFlow.Instance.GameTurn <= 0) return;
		_ = Announce(GameFlow.Instance.CurrentFaction);
	}

	private static void IgnoreMouseRecursively(Node node)
	{
		if (node is Control control) control.MouseFilter = MouseFilterEnum.Ignore;
		foreach (Node child in node.GetChildren()) IgnoreMouseRecursively(child);
	}

	/// <summary>
	/// Fade the badge in over the middle of the screen, hold it, and fade it out. Completes when the
	/// badge is gone again. A no-op when there is no badge in the tree, which is the headless case
	/// and the gap between game scenes.
	/// </summary>
	public static Task Announce(Faction faction)
	{
		TurnBadge badge = Instance;
		if (badge == null || !IsInstanceValid(badge)) return Task.CompletedTask;
		return badge.Run(faction);
	}

	private async Task Run(Faction faction)
	{
		// One announcement per turn. The turn change and the catch-up above can both reach here for
		// the opening turn, depending on which of them won the race to build this node.
		int turn = GameFlow.Instance?.GameTurn ?? -1;
		if (turn == _announcedTurn) return;
		_announcedTurn = turn;

		// GetValueOrDefault rather than FactionData.FlagTexture: that indexer throws for NONE and
		// ALL, and this runs off a turn boundary that error recovery can leave in an odd state.
		Flag.Texture = StaticGameData.FactionDataMap.GetValueOrDefault(faction)?.FlagTexture;
		Caption.Text = $"{faction.Label()}'s turn";

		// Turns can be skipped through faster than the badge runs. Killing the previous fade rather
		// than starting a second one alongside it is what stops two tweens writing modulate against
		// each other and leaving the badge parked at some half-faded alpha.
		_fade?.Kill();
		_fade?.Dispose();

		Modulate = new Color(Modulate, 0f);
		Show();

		// The slowest scale the project has, on all three phases. The badge blocks nothing and is
		// read rather than watched, so it is the one thing in the turn that can afford to take its
		// time — and it still collapses to nothing under a fast-forwarded restore, because every
		// Duration* property returns 0 there.
		double fadeIn  = GameSettings.DurationLongSeconds;
		double hold    = GameSettings.DurationLongSeconds;
		double fadeOut = GameSettings.DurationLongSeconds;

		Tween fade = CreateTween();
		fade.TweenProperty(this, "modulate:a", 1f, fadeIn)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		fade.TweenInterval(hold);
		fade.TweenProperty(this, "modulate:a", 0f, fadeOut)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
		_fade = fade;

		// Waited out on a scene tree timer rather than on the tween's Finished signal, because a
		// badge cut short by the next turn is Kill()ed and a killed tween never emits Finished —
		// awaiting that would leave this task pending for the rest of the session. The timer also
		// resumes on the main thread, which a bare Task.Delay would not.
		await ToSignal(GetTree().CreateTimer(fadeIn + hold + fadeOut), SceneTreeTimer.SignalName.Timeout);

		// The scene can go away across that wait, and a newer announcement can have taken the badge
		// over — in which case it owns the hide, and the disposal of its own tween, from here.
		if (!IsInstanceValid(this) || _fade != fade) return;

		Hide();
		fade.Dispose();
		_fade = null;
	}
}
