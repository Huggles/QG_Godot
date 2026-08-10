using Godot;

/// <summary>
/// The countdown on the host's input backstop: how long is left before an unanswered request releases
/// the prompt and asks the host whether to retry or skip it.
///
/// Exists because that deadline used to be invisible. A player deliberating past it simply lost the
/// prompt, and the turn advanced with nothing on screen having warned them.
///
/// Its own node rather than a label inside <see cref="TriggerContextDisplay"/>: that panel is only
/// visible when there is a card or Bulletin to explain the prompt, while every input request has a
/// deadline. Driven from <c>InputRequest.Execute()</c>, the one chokepoint every request passes through,
/// so it covers all twelve request subclasses without touching any of their Handle() methods.
///
/// It counts from when the prompt appears, while the host's clock starts when it sent the request — so it
/// reads slightly high by however long this peer's ChangeEventQueue took to drain first. Deliberate: the
/// alternative is a replicated deadline and a synchronised clock, for a discrepancy of seconds against a
/// window of minutes. If the two ever need to agree exactly, send a deadline rather than a duration.
/// </summary>
public partial class InputTimerDisplay : Control, LoadableUI
{
	public static InputTimerDisplay Current;

	/// <summary>Below this, the countdown turns red — the point where it is worth reacting to.</summary>
	private const double WarnSeconds = 60.0;

	private Panel Panel => GetNode<Panel>("%InputTimerPanel");
	private Label TitleLabel => GetNode<Label>("%InputTimerTitle");
	private Label TimeLabel => GetNode<Label>("%InputTimerValue");

	/// <summary>
	/// Seconds left, counted down in _Process rather than compared against a wall-clock deadline: the
	/// client has no synchronised clock with the host, and the value it is given is a duration ("you have
	/// 15 minutes"), not a timestamp. Negative means nothing is being timed.
	/// </summary>
	private double _remaining = -1;

	public override void _Ready()
	{
		// Same guard as TriggerContextDisplay: user_interface.tscn is instanced once per player, so only
		// the locally-controlled copy may claim Current or the timer would render on every player's UI.
		if (GetMultiplayerAuthority() == Multiplayer.GetUniqueId())
		{
			Current = this;
			LoadUI();
		}
	}

	public void LoadUI()
	{
		Panel.Visible = false;
		SetProcess(false);
	}

	/// <summary>
	/// Start counting down <paramref name="seconds"/> under <paramref name="title"/>. A second call
	/// replaces the first, which is what makes a retry restart the countdown for free.
	/// </summary>
	public void Start(int seconds, string title)
	{
		if (Panel == null) return;

		// Zero/absent means the sender had no deadline to report (e.g. a request built before this field
		// existed, or a replayed one). Showing "0:00" would read as "out of time".
		if (seconds <= 0)
		{
			Hide();
			return;
		}

		_remaining = seconds;
		TitleLabel.Text = title;
		Render();
		Panel.Visible = true;
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		if (_remaining < 0) return;

		_remaining -= delta;
		if (_remaining <= 0)
		{
			// Do not hide: the host is the authority on when the window closed, and it is about to abort
			// the request. Sitting at 0:00 is the honest reading until that arrives.
			_remaining = 0;
			SetProcess(false);
		}
		Render();
	}

	private void Render()
	{
		int total = Mathf.CeilToInt((float)_remaining);
		TimeLabel.Text = $"{total / 60}:{total % 60:00}";
		TimeLabel.Modulate = _remaining <= WarnSeconds ? Colors.OrangeRed : Colors.White;
	}

	public new void Hide()
	{
		_remaining = -1;
		SetProcess(false);
		if (Panel != null)
			Panel.Visible = false;
	}
}
