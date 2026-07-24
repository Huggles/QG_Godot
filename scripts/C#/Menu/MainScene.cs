using Godot;
using System.Linq;

public partial class MainScene : Node
{
    public override void _Ready()
    {
        var userArgs = OS.GetCmdlineUserArgs();

        // Go straight to the multiplayer lobby for: a dedicated/headless server (auto-hosts there),
        // an auto-joining test client, or the F6 debug flow. Everything else opens the main menu.
        bool toLobby = GameContext.IsHeadless
                       || userArgs.Contains("is_debug_multiplayer=true")
                       || userArgs.Contains("auto_join=true");

        string targetScene = toLobby
            ? "res://scenes/menu/MultiplayerLobby.tscn"
            : "res://scenes/menu/Menu.tscn";

        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, targetScene);
    }
}
