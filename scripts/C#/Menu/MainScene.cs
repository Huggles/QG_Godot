using Godot;
using System.Linq;

public partial class MainScene : Node
{
    public override void _Ready()
    {
        string targetScene = OS.GetCmdlineUserArgs().Contains("is_debug_multiplayer=true")
            ? "res://scenes/menu/MultiplayerLobby.tscn"
            : "res://scenes/menu/Menu.tscn";

        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, targetScene);
    }
}
