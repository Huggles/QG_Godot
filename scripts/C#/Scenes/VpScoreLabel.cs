using Godot;
using System.Threading.Tasks;

/// <summary>
/// The floating +N / -N shown where VP was scored, shared by countries and units. Placed by the scene
/// that instances it; <see cref="Play"/> always restarts from that authored spot.
/// </summary>
public partial class VpScoreLabel : Label
{
	private const float HoldSeconds = 1f;
	private const float FadeSeconds = 1f;
	private const float RiseDistance = 150f;
	private static readonly Color GainColor = new Color(0.35f, 1f, 0.35f);
	private static readonly Color LossColor = new Color(1f, 0.3f, 0.3f);

	private Vector2 restPosition;
	private Tween tween;
	private TaskCompletionSource done;

	public override void _Ready()
	{
		restPosition = Position;
		Visible = false;
	}

	public override void _ExitTree()
	{
		// Leaving the tree kills the tween, so its callback would never release whoever awaits it.
		done?.TrySetResult();
	}

	/// <summary>
	/// Holds, then drifts up while fading out. Calling again mid-animation restarts it; the returned
	/// task completes when the label is gone or superseded.
	/// </summary>
	public Task Play(int vp)
	{
		tween?.Kill();
		// A killed tween never reaches its callback, so release whoever awaited the old run here.
		done?.TrySetResult();
		TaskCompletionSource current = done = new TaskCompletionSource();

		// A negative number already carries its own '-'. The text is white, so Modulate tints it and the outline stays black.
		Text = vp > 0 ? $"+{vp}" : vp.ToString();
		Modulate = vp > 0 ? GainColor : vp < 0 ? LossColor : Colors.White;
		Position = restPosition;
		Visible = true;

		tween = CreateTween();
		tween.TweenInterval(HoldSeconds);
		tween.TweenProperty(this, "position:y", restPosition.Y - RiseDistance, FadeSeconds);
		tween.Parallel().TweenProperty(this, "modulate:a", 0f, FadeSeconds);
		tween.TweenCallback(Callable.From(() =>
		{
			Visible = false;
			current.TrySetResult();
		}));
		return current.Task;
	}
}
