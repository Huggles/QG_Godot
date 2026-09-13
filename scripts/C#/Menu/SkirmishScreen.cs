using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using static FactionMenuVisuals;

/// <summary>
/// Skirmish setup: pick a scenario, a seed, and which factions YOU play. Every faction left unpicked
/// is played by a bot.
///
/// Offline by construction — one peer holding the human's factions plus one AI seat per bot faction,
/// no lobby, no socket. That is why this is not the multiplayer lobby with its networking turned off:
/// that screen keys its rows by peer id, creates them only from connection events, and gates its Start
/// button on a flag set inside CreateServer. Here there is exactly one human, so there is nothing to
/// synchronise and no coverage to validate — an unpicked faction is a bot rather than a hole.
///
/// The seats are the whole feature; see <see cref="BuildAssignments"/> for how they reach the game, and
/// PlayerFactionRegistry.AiSeatIdBase for why a bot seat is invisible to this player's HUD.
/// </summary>
public partial class SkirmishScreen : Control
{
	/// <summary>
	/// Menu.tscn, not MainMenu.tscn: the latter is the bare Control without the Camera2D and
	/// CanvasLayer wrapper, so it comes up unrendered.
	/// </summary>
	private const string MenuScenePath = "res://scenes/menu/Menu.tscn";

	/// <summary>
	/// Height of a seat's flag. Smaller than the lobby's FlagHeight because a seat here is a whole ROW
	/// rather than one of six flags within one, so six of them have to fit down this screen.
	/// </summary>
	private const int SeatFlagHeight = 44;

	private OptionButton _scenarioPicker;
	private RichTextLabel _descriptionLabel;
	private MenuPanelButton _startGameButton;
	private MenuPanelButton _backButton;
	private LineEdit _seedInput;
	private Button _randomizeSeedButton;
	private GridContainer _seatGrid;
	private Label _seatsLabel;
	private CheckBox _openingDiscardCheckBox;

	/// <summary>
	/// The factions the human will play. Everything not in here is a bot, so this one set is the whole
	/// model — no dictionaries keyed by peer, because there is only ever one human on this screen.
	///
	/// Starts holding every faction, which makes "Skirmish then Start" identical to the single-player
	/// game this screen used to start. Deselect to hand seats to bots.
	/// </summary>
	private readonly HashSet<Faction> _humanFactions = new(StaticGameData.PlayableFactions);

	private readonly Dictionary<Faction, Seat> _seats = new();
	private Faction? _hovered;

	/// <summary>
	/// True while the picked scenario runs a tutorial script, which plays every faction itself.
	///
	/// A lock rather than a filter: this screen's picker is the only way to start the tutorial in the
	/// whole GUI, so hiding tutorials here would fix the bot problem by deleting the feature. Instead
	/// the seats go read-only and every faction is the human's — which is exactly what this screen did
	/// before it had a grid, so the tutorial path is unchanged.
	///
	/// It also keeps AiSeatRuntime away from its own refusal branch: with no AI seats to install it
	/// never reaches the "a tutorial is armed" error, which is a message a player cannot act on.
	///
	/// Lives in the EFFECTIVE view rather than in <see cref="_humanFactions"/>, so a player's picks
	/// survive selecting a tutorial and switching back.
	/// </summary>
	private bool _scriptedScenario;

	/// <summary>
	/// Set once a scene change is on its way, and never cleared — this node is leaving.
	///
	/// Needed because MenuPanelButton emits Pressed even while Disabled (its _GuiInput blocks the mouse
	/// but keyboard activation still fires) and SceneFlow.ChangeScene is DEFERRED, so two fast Enter
	/// presses would commit the seed twice, rewrite the pending assignments and queue two scene
	/// changes. Shared with Back so Start-then-Back cannot queue two in opposite directions.
	/// </summary>
	private bool _starting;

	/// <summary>The widgets of one seat row, so <see cref="RenderSeat"/> can restyle it in place.</summary>
	private sealed class Seat
	{
		public TextureRect Flag;
		public Label Controller;
		public Button Click;
	}

	// Scene wiring: a GetNode failure here means a broken .tscn, which is a real bug worth
	// surfacing rather than a silent console line.
	public override void _Ready() => Guard.Try(ReadyInternal, "SkirmishScreen._Ready");

	private void ReadyInternal()
	{
		_scenarioPicker   = GetNode<OptionButton>("ButtonContainer/ScenarioOptionButton");
		_descriptionLabel = GetNodeOrNull<RichTextLabel>("ButtonContainer/DescriptionPanel/ScenarioDescriptionLabel");
		if (_descriptionLabel == null)
		{
			GD.PrintErr("SkirmishScreen: scenario description label node not found.");
		}

		_startGameButton  = GetNode<MenuPanelButton>("ButtonContainer/StartGameButton");
		_backButton       = GetNode<MenuPanelButton>("ButtonContainer/BackButton");
		_seatGrid         = GetNode<GridContainer>("ButtonContainer/SeatGrid");
		_seatsLabel       = GetNode<Label>("ButtonContainer/SeatsLabel");
		_openingDiscardCheckBox = GetNode<CheckBox>("ButtonContainer/OpeningDiscardCheckBox");

		_seedInput           = GetNode<LineEdit>("ButtonContainer/SeedRow/SeedInput");
		_randomizeSeedButton = GetNode<Button>("ButtonContainer/SeedRow/RandomizeSeedButton");
		MenuSeedField.Bind(_seedInput, _randomizeSeedButton);

		_startGameButton.ButtonText = "Start Game";
		_backButton.ButtonText = "Back";

		var gameManager = GetNode<GameManager>("/root/GameManager");

		// Tutorials are kept, unlike the lobby's picker: this is the only route to one in the GUI. A
		// scripted scenario locks the seat grid instead — see _scriptedScenario.
		MenuScenarioPicker.Populate(_scenarioPicker, gameManager.AvailableScenarios, includeTutorials: true);

		if (gameManager.SelectedScenario != null)
		{
			MenuScenarioPicker.SelectByPath(
				_scenarioPicker, gameManager.AvailableScenarios, gameManager.SelectedScenario.Path);
			UpdateDescription(gameManager.SelectedScenario.Description);
			_scriptedScenario = gameManager.SelectedScenario.IsTutorial;
			_openingDiscardCheckBox.ButtonPressed = gameManager.SelectedScenario.OpeningDiscard;
		}
		else
		{
			UpdateDescription(MenuScenarioPicker.NoScenariosMessage);
		}

		BuildSeats();

		_scenarioPicker.ItemSelected += OnScenarioSelected;
		_startGameButton.Pressed += OnStartGamePressed;
		_backButton.Pressed += OnBackPressed;
	}

	// ── The seat grid ─────────────────────────────────────────────────────────

	/// <summary>
	/// Build the six seat rows, in code for the same reason MultiplayerLobby.AddPlayerRow builds its
	/// own: a repeated faction row is data, and hand-authoring two dozen nodes into a .tscn means
	/// inventing two dozen non-colliding unique_ids by hand.
	///
	/// Two columns filled row-major, and the loop adds an Axis faction then an Allied one, so the
	/// columns come out Axis-left and Allies-right — the way the board reads.
	/// </summary>
	private void BuildSeats()
	{
		_seatGrid.Columns = 2;

		List<Faction> axis   = AllPlayableFactions.Where(AxisSet.Contains).ToList();
		List<Faction> allies = AllPlayableFactions.Where(faction => !AxisSet.Contains(faction)).ToList();

		for (int i = 0; i < axis.Count; i++)
		{
			AddSeat(axis[i]);
			if (i < allies.Count) AddSeat(allies[i]);
		}

		RenderAllSeats();
	}

	/// <inheritdoc cref="BuildSeats"/>
	private void AddSeat(Faction faction)
	{
		PanelContainer panel = new();
		StyleBoxFlat style = new()
		{
			BgColor = new Color(0.15f, 0.15f, 0.15f, 0.55f),
			CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
			ContentMarginLeft = 8, ContentMarginRight = 8,
			ContentMarginTop = 4, ContentMarginBottom = 4,
		};
		panel.AddThemeStyleboxOverride("panel", style);

		// Ignore, so the click overlay added last gets the mouse rather than these labels eating it.
		HBoxContainer row = new() { MouseFilter = MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 10);

		TextureRect flag = new()
		{
			Texture = GD.Load<Texture2D>(FlagPaths[faction]),
			CustomMinimumSize = new Vector2(0, SeatFlagHeight),
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
		};

		Label name = new()
		{
			Text = FactionNames[faction],
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center,
		};

		Label controller = new()
		{
			CustomMinimumSize = new Vector2(72, 0),
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
		};

		row.AddChild(flag);
		row.AddChild(name);
		row.AddChild(controller);
		panel.AddChild(row);

		// Added last so it draws and hits on top of the whole row, which also buys focus traversal and
		// a tooltip for free. A PanelContainer sizes every child to its content rect, so this covers
		// the row exactly rather than needing its own layout.
		Button click = new() { Flat = true, TooltipText = $"Play as {FactionNames[faction]}" };
		click.Pressed += () => OnSeatPressed(faction);
		click.MouseEntered += () => { _hovered = faction; RenderSeat(faction); };
		click.MouseExited += () =>
		{
			if (_hovered == faction) _hovered = null;
			RenderSeat(faction);
		};
		panel.AddChild(click);

		_seatGrid.AddChild(panel);
		_seats[faction] = new Seat { Flag = flag, Controller = controller, Click = click };
	}

	/// <summary>
	/// Who plays this faction, once the scripted-scenario lock is taken into account.
	///
	/// The seam the lock lives on: <see cref="_humanFactions"/> keeps the player's real picks untouched
	/// while a tutorial is selected, so switching back to an ordinary scenario restores them.
	/// </summary>
	private bool EffectiveHuman(Faction faction)
		=> _scriptedScenario || _humanFactions.Contains(faction);

	private void OnSeatPressed(Faction faction)
	{
		if (_scriptedScenario) return;

		if (!_humanFactions.Remove(faction)) _humanFactions.Add(faction);
		RenderSeat(faction);
	}

	private void RenderAllSeats()
	{
		foreach (Faction faction in _seats.Keys) RenderSeat(faction);

		if (_seatsLabel != null)
			_seatsLabel.Text = _scriptedScenario
				? "This scenario is scripted and plays every faction"
				: "Your Factions";
	}

	/// <summary>
	/// Restyle one seat from the model. Whole-seat rather than incremental: there are six of them and a
	/// click changes one, so there is nothing to gain from being clever and a state that can drift from
	/// the model to lose.
	///
	/// The colours are the lobby's, and the mapping is exact rather than approximate: a bot seat is
	/// literally "owned by another player", and the hover white is literally "available to you".
	/// ColUnavailable ("wrong team") has no meaning here — a Skirmish has no team constraint, which is
	/// the point of it — so it goes unused rather than being given an invented role.
	///
	/// "AI" is the string FactionDisplay.PlayerNameFor already returns for a bot faction, and so what
	/// puts "Germany (AI)" on the history rows and banners in game. This screen matching it exactly is
	/// deliberate; it must not say "Bot" or "Computer".
	/// </summary>
	private void RenderSeat(Faction faction)
	{
		if (!_seats.TryGetValue(faction, out Seat seat)) return;

		bool mine = EffectiveHuman(faction);

		seat.Controller.Text = mine ? "You" : "AI";
		seat.Flag.Modulate = mine
			? ColClaimed
			: (_hovered == faction ? ColAvailable : ColOtherOwned);
		seat.Controller.Modulate = seat.Flag.Modulate;

		// Read-only while a tutorial is selected. Disabled rather than merely ignored in OnSeatPressed
		// so the row stops taking focus and its hover tint stops implying a choice that is not there.
		seat.Click.Disabled = _scriptedScenario;
	}

	// ── Scenario, seed, start ─────────────────────────────────────────────────

	private void OnScenarioSelected(long selectedIndex)
	{
		var gameManager = GetNode<GameManager>("/root/GameManager");
		// The item's id, not its position — see MenuScenarioPicker. This picker happens not to filter,
		// so the two agree today; asking for the id anyway is what keeps that a fact about the data
		// rather than a coincidence nobody would notice breaking.
		int index = MenuScenarioPicker.ScenarioIndexAt(_scenarioPicker, (int)selectedIndex);
		if (index < 0 || index >= gameManager.AvailableScenarios.Count)
			return;

		gameManager.SetSelectedScenarioByIndex(index);
		UpdateDescription(gameManager.AvailableScenarios[index].Description);

		// A plain assignment, deliberately NOT the lobby's SetOpeningDiscard unsubscribe/resubscribe
		// dance: that exists because the lobby's box has a Toggled handler that RPCs to clients, so
		// seeding it must not read as a host toggling it. Nothing is subscribed here — no clients, no
		// RPC — so there is nothing to suppress.
		_openingDiscardCheckBox.ButtonPressed = gameManager.AvailableScenarios[index].OpeningDiscard;

		_scriptedScenario = gameManager.AvailableScenarios[index].IsTutorial;
		RenderAllSeats();
	}

	private void OnStartGamePressed()
	{
		if (_starting) return;

		var gameManager = GetNode<GameManager>("/root/GameManager");
		if (gameManager.SelectedScenario == null)
		{
			UpdateDescription("No scenario selected.");
			return;
		}

		_starting = true;
		StartGame(gameManager.SelectedScenario.Path);
	}

	private void OnBackPressed()
	{
		if (_starting) return;
		_starting = true;
		SceneFlow.ChangeScene(this, MenuScenePath);
	}

	private void UpdateDescription(string description)
	{
		MenuScenarioPicker.ShowDescription(_descriptionLabel, description);
	}

	private void StartGame(string scenarioPath)
	{
		GameManager.PendingScenarioPath = scenarioPath;
		MenuSeedField.Commit(_seedInput);
		// Always a definite bool, never null. SetSelectedScenarioByIndex clears this static on every
		// picker change, but it only runs when the player actually CHANGES the picker — so writing the
		// box's value unconditionally is also what stops an override left over from a lobby session
		// earlier in the same process from leaking into this game.
		GameManager.PendingOpeningDiscard = _openingDiscardCheckBox.ButtonPressed;

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
	/// The seating: the human on peer 1, and one AI seat per faction they did not pick.
	///
	/// One assignment per bot rather than one shared one, deliberately — each becomes its own
	/// PlayerScene with its own decision stream, so two bots are two players rather than one object
	/// answering twice. Those synthetic peer ids are also what keep a bot's hand off this player's
	/// screen; see PlayerFactionRegistry.AiSeatIdBase.
	///
	/// Both lists filter StaticGameData.PlayableFactions — TURN order — and not the grid's team-grouped
	/// display order. Two reasons: picking every faction then produces the identical list the old
	/// single-player path produced, and an AI seat's id is decided by turn order rather than by the
	/// order the player happened to click.
	///
	/// Peer 1's assignment is kept even when it holds NO factions. Omitting it would leave
	/// PlayerScene.Current null, which is survivable headless but means no HUD and no camera — and
	/// watching six bots play is a case worth being able to reach.
	///
	/// DisplayName stays null on every seat. With at least one bot the game is no longer "single
	/// player", so a human faction falls through FactionDisplay.PlayerNameFor to a null display name and
	/// renders as bare "Germany", while a bot renders "Germany (AI)". That asymmetry is exactly right,
	/// and it is free only as long as nothing here invents a name.
	/// </summary>
	private List<PlayerFactionAssignment> BuildAssignments()
	{
		List<Faction> humanFactions = StaticGameData.PlayableFactions
			.Where(EffectiveHuman)
			.ToList();
		List<Faction> aiFactions = StaticGameData.PlayableFactions
			.Where(faction => !EffectiveHuman(faction))
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
}
