using Godot;

/// <summary>
/// Beta terms shown on startup, ahead of the main menu: <see cref="MainScene"/> boots into this
/// screen and Accept hands over to <c>Menu.tscn</c>, Decline closes the game.
///
/// Shown on every launch rather than remembered in <see cref="GameSettings"/> — testers should meet
/// the terms each session, and a stored flag would also survive into builds the terms change in.
///
/// Only the interactive boot path comes through here. A CLI run, a dedicated server and the debug
/// multiplayer flow all route past it in <see cref="MainScene"/>: nobody is there to press a button,
/// and a prompt would simply hang them.
/// </summary>
public partial class DisclaimerScreen : Control
{
	private const string MenuScenePath = "res://scenes/menu/Menu.tscn";

	/// <summary>
	/// Set the moment a choice is made. <see cref="MenuPanelButton"/> emits Pressed from _GuiInput, so
	/// a double click arrives as two presses and would otherwise queue a second scene change.
	/// </summary>
	private bool _resolved;

	// Scene wiring: a GetNode failure here means a broken .tscn, which is a real bug worth
	// surfacing rather than a silent console line.
	public override void _Ready() => Guard.Try(ReadyInternal, "DisclaimerScreen._Ready");

	private void ReadyInternal()
	{
		var accept  = GetNode<MenuPanelButton>("%AcceptButton");
		var decline = GetNode<MenuPanelButton>("%DeclineButton");

		accept.ButtonText  = "Accept";
		decline.ButtonText = "Decline";

		accept.Pressed  += OnAcceptPressed;
		decline.Pressed += OnDeclinePressed;
	}

	/// <summary>
	/// Through SceneFlow rather than ChangeSceneToFile, so the shutting-down flag is set before this
	/// scene is freed. The menu music started at boot carries on: PlayMusic is a no-op for the track
	/// already playing.
	/// </summary>
	private void OnAcceptPressed()
	{
		if (_resolved) return;
		_resolved = true;

		SceneFlow.ChangeScene(this, MenuScenePath);
	}

	/// <summary>
	/// Declining the terms means not playing, so this is the one place besides the menu's Quit button
	/// that ends the process. Deliberately not bound to Escape — closing the game must be a deliberate
	/// press, not a reflex.
	/// </summary>
	private void OnDeclinePressed()
	{
		if (_resolved) return;
		_resolved = true;

		GetTree().Quit();
	}
}
