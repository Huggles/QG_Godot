using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The Tutorials menu: one button per tutorial scenario, in title order, each starting its game
/// directly. A tutorial script dictates the whole setup, so there is no Skirmish-style screen between.
/// </summary>
public partial class TutorialScreen : Control
{
	private const string MenuScenePath = "res://scenes/menu/Menu.tscn";
	private const string CommandButtonScenePath = "res://scenes/menu/MenuCommandButton.tscn";

	/// <summary>Set once a scene change is queued; MenuPanelButton emits Pressed even while Disabled.</summary>
	private bool _starting;

	public override void _Ready() => Guard.Try(ReadyInternal, "TutorialScreen._Ready");

	private void ReadyInternal()
	{
		var gameManager = GetNode<GameManager>("/root/GameManager");
		var list = GetNode<VBoxContainer>("%TutorialList");
		var buttonScene = GD.Load<PackedScene>(CommandButtonScenePath);

		List<ScenarioInfo> tutorials = gameManager.AvailableScenarios.Where(s => s.IsTutorial).ToList();
		foreach (ScenarioInfo scenario in tutorials)
		{
			var button = buttonScene.Instantiate<MenuPanelButton>();
			button.ButtonText = scenario.Title;
			button.Pressed += () => StartTutorial(scenario);
			list.AddChild(button);

			if (!string.IsNullOrEmpty(scenario.Description))
				list.AddChild(Indented(new Label
				{
					Text = scenario.Description,
					ThemeTypeVariation = "MenuMuted",
					AutowrapMode = TextServer.AutowrapMode.WordSmart,
				}));
		}

		if (tutorials.Count == 0)
			list.AddChild(new Label { Text = "No tutorials found.", ThemeTypeVariation = "MenuMuted" });

		var back = GetNode<MenuPanelButton>("%BackButton");
		back.ButtonText = "Back";
		back.Pressed += OnBackPressed;
	}

	/// <summary>Lines a description up under its button's title, with a gap before the next entry.</summary>
	private static MarginContainer Indented(Control child)
	{
		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 26);
		margin.AddThemeConstantOverride("margin_bottom", 10);
		margin.AddChild(child);
		return margin;
	}

	/// <summary>
	/// The Skirmish screen's start, minus the choices: every faction is the player's (the script plays
	/// the rest) and the scenario's own opening-discard and expansions stand.
	/// </summary>
	private void StartTutorial(ScenarioInfo scenario)
	{
		if (_starting) return;
		_starting = true;

		var gameManager = GetNode<GameManager>("/root/GameManager");
		gameManager.SetSelectedScenarioByIndex(gameManager.AvailableScenarios.IndexOf(scenario));
		GameManager.PendingSeed = MenuSeedField.Roll();
		GameManager.PendingOpeningDiscard = scenario.OpeningDiscard;
		GameManager.PendingExpansions = new List<string>(scenario.Expansions);
		gameManager.SetPendingPlayerFactionAssignments(new List<PlayerFactionAssignment>
		{
			new(1, StaticGameData.PlayableFactions.ToList()),
		});

		// See SkirmishScreen.StartGame: without a peer the readiness barrier hangs. Must precede the scene change.
		Multiplayer.MultiplayerPeer ??= new OfflineMultiplayerPeer();
		SceneFlow.ChangeScene(this, SceneFlow.GameScenePath);
	}

	private void OnBackPressed()
	{
		if (_starting) return;
		_starting = true;
		SceneFlow.ChangeScene(this, MenuScenePath);
	}
}
