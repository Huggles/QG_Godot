using Godot;
using System.Collections.Generic;

public partial class GameModeSelectionScreen : Control
{
    private const string ScenarioBasicPath = "res://assets/data/Scenario_Basic.json";
    private const string ScenarioDebugPath = "res://assets/data/Scenario_Debug.json";

    public override void _Ready()
    {
        var standardBtn = GetNode<MenuPanelButton>("%StandardGameButton");
        var debugBtn    = GetNode<MenuPanelButton>("%DebugScenarioButton");

        standardBtn.CustomMinimumSize = new Vector2(600, 180);
        debugBtn.CustomMinimumSize    = new Vector2(600, 180);

        standardBtn.ButtonText =
            "[b][font_size=28]Standard Game[/font_size][/b]\n" +
            "[color=#bbbbbb][font_size=18]All factions deployed to home territories.\n" +
            "Classic setup — no pre-loaded cards.[/font_size][/color]";

        debugBtn.ButtonText =
            "[b][font_size=28]Debug Scenario[/font_size][/b]\n" +
            "[color=#bbbbbb][font_size=18]Germany starts with pre-loaded status cards.\n" +
            "No unit deployments — ideal for testing card effects.[/font_size][/color]";

        standardBtn.Pressed += () => StartGame(ScenarioBasicPath);
        debugBtn.Pressed    += () => StartGame(ScenarioDebugPath);
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
