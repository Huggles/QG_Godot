using Godot;
using System.Collections.Generic;

public partial class GameModeSelectionScreen : Control
{
    private const string ScenarioBasicPath    = "res://assets/data/Scenario_Basic.json";
    private const string ScenarioDebugPath    = "res://assets/data/Scenario_Debug.json";
    private const string ScenarioAllCardsPath = "res://assets/data/Scenario_AllCards.json";
    private const string ScenarioOneRoundPath = "res://assets/data/Scenario_OneRound.json";

    public override void _Ready()
    {
        var standardBtn  = GetNode<MenuPanelButton>("%StandardGameButton");
        var debugBtn     = GetNode<MenuPanelButton>("%DebugScenarioButton");
        var allCardsBtn  = GetNode<MenuPanelButton>("%AllCardsButton");
        var oneRoundBtn  = GetNode<MenuPanelButton>("%OneRoundButton");

        standardBtn.CustomMinimumSize = new Vector2(600, 180);
        debugBtn.CustomMinimumSize    = new Vector2(600, 180);
        allCardsBtn.CustomMinimumSize = new Vector2(600, 180);
        oneRoundBtn.CustomMinimumSize = new Vector2(600, 180);

        standardBtn.ButtonText =
            "[b][font_size=28]Standard Game[/font_size][/b]\n" +
            "[color=#bbbbbb][font_size=18]All factions deployed to home territories.\n" +
            "Classic setup — no pre-loaded cards.[/font_size][/color]";

        debugBtn.ButtonText =
            "[b][font_size=28]Debug Scenario[/font_size][/b]\n" +
            "[color=#bbbbbb][font_size=18]Germany starts with pre-loaded status cards.\n" +
            "No unit deployments — ideal for testing card effects.[/font_size][/color]";

        allCardsBtn.ButtonText =
            "[b][font_size=28]All Cards In Play[/font_size][/b]\n" +
            "[color=#bbbbbb][font_size=18]Every faction's status and response cards are already played.\n" +
            "Home territory deployments — ideal for testing card interactions.[/font_size][/color]";

        oneRoundBtn.ButtonText =
            "[b][font_size=28]One Round Blitz[/font_size][/b]\n" +
            "[color=#bbbbbb][font_size=18]A single round — every faction starts with random VP.\n" +
            "Home territory deployments — a quick, chaotic scoring race.[/font_size][/color]";

        standardBtn.Pressed  += () => StartGame(ScenarioBasicPath);
        debugBtn.Pressed     += () => StartGame(ScenarioDebugPath);
        allCardsBtn.Pressed  += () => StartGame(ScenarioAllCardsPath);
        oneRoundBtn.Pressed  += () => StartGame(ScenarioOneRoundPath);
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
