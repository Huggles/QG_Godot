using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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

		_startGameButton  = GetNode<MenuPanelButton>("ButtonContainer/StartGameButton");

		_seedInput           = GetNode<LineEdit>("ButtonContainer/SeedRow/SeedInput");
		_randomizeSeedButton = GetNode<Button>("ButtonContainer/SeedRow/RandomizeSeedButton");
		MenuSeedField.Bind(_seedInput, _randomizeSeedButton);
		
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
		if (_descriptionLabel == null)
			return;

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

		GetNode<GameManager>("/root/GameManager").SetPendingPlayerFactionAssignments(BuildAssignments());

		// Without a peer, get_unique_id() is 0 → IsServer() is false → PeerReadinessComponent takes its
		// client branch and the report dies on Godot's "no multiplayer peer is active" guard, hanging
		// this path at the barrier forever. OfflineMultiplayerPeer gives unique id 1 and no peers, so
		// the barrier expects exactly one and fires immediately. See CliBootstrap, which does the same.
		// Must precede the scene change: the barrier goes up as soon as Game.tscn loads.
		Multiplayer.MultiplayerPeer ??= new OfflineMultiplayerPeer();

		SceneFlow.ChangeScene(this, SceneFlow.GameScenePath);
	}

	/// <summary>
	/// Seats for a single-player game: the player on peer 1, and one AI seat per faction named by
	/// <c>ai_factions</c>.
	///
	/// The command-line source is TEMPORARY — a picker on this screen replaces it once the difficulty
	/// selection lands. It exists so the AI seats can be played and watched before any UI is built,
	/// which is the only way to find out how the pacing actually feels. `MultiplayerLobby` reads
	/// `load=` the same way.
	///
	/// <code>ai_factions=JAPAN,ITALY,GERMANY</code>
	///
	/// One assignment per AI faction rather than one shared one, so each gets its own PlayerScene and
	/// therefore its own decision stream — see PlayerFactionRegistry.AiSeatIdBase for why a synthetic
	/// peer id is what makes those scenes invisible to the human's HUD.
	/// </summary>
	private static List<PlayerFactionAssignment> BuildAssignments()
	{
		List<Faction> aiFactions = ParseAiFactions();
		List<Faction> humanFactions = StaticGameData.PlayableFactions
			.Where(faction => !aiFactions.Contains(faction))
			.ToList();

		List<PlayerFactionAssignment> assignments = new()
		{
			new PlayerFactionAssignment(1, humanFactions),
		};

		for (int i = 0; i < aiFactions.Count; i++)
		{
			assignments.Add(new PlayerFactionAssignment(
				PlayerFactionRegistry.AiSeatIdBase + i,
				new List<Faction> { aiFactions[i] }));
		}

		return assignments;
	}

	/// <inheritdoc cref="BuildAssignments"/>
	private static List<Faction> ParseAiFactions()
	{
		string spec = CliArgs.Get("ai_factions");
		if (string.IsNullOrWhiteSpace(spec)) return new List<Faction>();

		List<Faction> parsed = new();
		foreach (string name in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			if (Enum.TryParse(name, ignoreCase: true, out Faction faction)
			    && StaticGameData.PlayableFactions.Contains(faction))
			{
				parsed.Add(faction);
			}
			else
			{
				// Loud, and it does not fall back to a human seat silently: a typo here means the game
				// you play is not the game you asked for, which is the same reason bot_rules refuses an
				// unknown rule name rather than ignoring it.
				DebugUtilities.PrintPeerErrorRaw(
					$"ai_factions: '{name}' is not a playable faction. Known: " +
					string.Join(", ", StaticGameData.PlayableFactions));
			}
		}
		return parsed.Distinct().ToList();
	}
}
