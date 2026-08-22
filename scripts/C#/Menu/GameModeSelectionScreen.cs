using Godot;
using System.Collections.Generic;

public partial class GameModeSelectionScreen : Control
{
	private OptionButton _scenarioPicker;
	private RichTextLabel _descriptionLabel;
	private MenuPanelButton _startGameButton;
	private LineEdit _seedInput;
	private Button _randomizeSeedButton;

	// Scene wiring: a GetNode failure here means a broken .tscn, which is a real bug worth
	// surfacing rather than a silent console line.
	public override void _Ready() => Guard.Try(ReadyInternal, "GameModeSelectionScreen._Ready");

	private void ReadyInternal()
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

		_seedInput           = GetNode<LineEdit>("ButtonContainer/SeedRow/SeedInput");
		_randomizeSeedButton = GetNode<Button>("ButtonContainer/SeedRow/RandomizeSeedButton");
		MenuSeedField.Bind(_seedInput, _randomizeSeedButton);

		_startGameButton.CustomMinimumSize = new Vector2(600, 180);
		_startGameButton.ButtonText = "Start Game";

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
		MenuSeedField.Commit(_seedInput);
		// This screen offers no opening-discard toggle, so the scenario's own answer always wins.
		// Explicit rather than relying on SetSelectedScenarioByIndex, which only runs if the player
		// actually changed the picker — an override left over from a lobby session earlier in the
		// same process would otherwise still be sitting in the static.
		GameManager.PendingOpeningDiscard = null;

		var assignments = new List<PlayerFactionAssignment>
		{
			new PlayerFactionAssignment(1, new List<Faction>(StaticGameData.PlayableFactions))
		};
		GetNode<GameManager>("/root/GameManager").SetPendingPlayerFactionAssignments(assignments);

		// Without a peer, get_unique_id() is 0 → IsServer() is false → PeerReadinessComponent takes its
		// client branch and the report dies on Godot's "no multiplayer peer is active" guard, hanging
		// this path at the barrier forever. OfflineMultiplayerPeer gives unique id 1 and no peers, so
		// the barrier expects exactly one and fires immediately. See CliBootstrap, which does the same.
		// Must precede the scene change: the barrier goes up as soon as Game.tscn loads.
		Multiplayer.MultiplayerPeer ??= new OfflineMultiplayerPeer();

		SceneFlow.ChangeScene(this, SceneFlow.GameScenePath);
	}
}
