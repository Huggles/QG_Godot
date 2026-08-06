using Godot;
using System.Collections.Generic;

public partial class GameModeSelectionScreen : Control
{
	private OptionButton _scenarioPicker;
	private RichTextLabel _descriptionLabel;
	private MenuPanelButton _startGameButton;

	public override void _Ready()
	{
		_scenarioPicker   = GetNode<OptionButton>("ButtonContainer/ScenarioOptionButton");
		_descriptionLabel = GetNodeOrNull<RichTextLabel>("ButtonContainer/DescriptionPanel/ScenarioDescriptionLabel");
		if (_descriptionLabel == null)
		{
			GD.PrintErr("GameModeSelectionScreen: scenario description label node not found.");
		}
		if (_descriptionLabel == null)
		{
			GD.PrintErr("GameModeSelectionScreen: scenario description label node not found.");
		}

		_startGameButton  = GetNode<MenuPanelButton>("ButtonContainer/StartGameButton");

		_startGameButton.CustomMinimumSize = new Vector2(600, 180);
		_startGameButton.ButtonText = "[b][font_size=28]Start Game[/font_size][/b]";

		var gameManager = GetNode<GameManager>("/root/GameManager");
		_scenarioPicker.Clear();

		foreach (var scenario in gameManager.AvailableScenarios)
		{
			_scenarioPicker.AddItem(scenario.Title);
		}

		if (gameManager.SelectedScenario != null)
		{
			int selectedIndex = gameManager.AvailableScenarios.FindIndex(s => s.Path == gameManager.SelectedScenario.Path);
			if (selectedIndex >= 0)
				_scenarioPicker.Selected = selectedIndex;

			UpdateDescription(gameManager.SelectedScenario.Description);
		}
		else
		{
			UpdateDescription("No scenarios available. Make sure the scenario files are present in assets/data/scenarios.");
		}

		_scenarioPicker.ItemSelected += OnScenarioSelected;
		_startGameButton.Pressed += OnStartGamePressed;
	}

	private void OnScenarioSelected(long selectedIndex)
	{
		var gameManager = GetNode<GameManager>("/root/GameManager");
		int index = (int)selectedIndex;
		if (index < 0 || index >= gameManager.AvailableScenarios.Count)
			return;

		gameManager.SetSelectedScenarioByIndex(index);
		UpdateDescription(gameManager.AvailableScenarios[index].Description);
	}

	private void OnStartGamePressed()
	{
		var gameManager = GetNode<GameManager>("/root/GameManager");
		if (gameManager.SelectedScenario == null)
		{
			UpdateDescription("No scenario selected.");
			return;
		}

		StartGame(gameManager.SelectedScenario.Path);
	}

	private void UpdateDescription(string description)
	{
		_descriptionLabel.BbcodeEnabled = true;
		_descriptionLabel.Text = $"[color=#bbbbbb]{description}[/color]";
	}

	private void StartGame(string scenarioPath)
	{
		GameManager.PendingScenarioPath = scenarioPath;

		var assignments = new List<PlayerFactionAssignment>
		{
			new PlayerFactionAssignment(1, new List<Faction>(StaticGameData.PlayableFactions))
		};
		GetNode<GameManager>("/root/GameManager").SetPendingPlayerFactionAssignments(assignments);
		GetTree().ChangeSceneToFile("res://scenes/Game.tscn");
	}
}
