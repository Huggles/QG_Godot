using Godot;
using System.Linq;

public partial class MainScene : Node
{
    // Scene wiring: a GetNode failure here means a broken .tscn, which is a real bug worth
    // surfacing rather than a silent console line.
    public override void _Ready() => Guard.Try(ReadyInternal, "MainScene._Ready");

    private void ReadyInternal()
    {
        var userArgs = OS.GetCmdlineUserArgs();

        // A CLI run bypasses the lobby entirely: it is a single process controlling every faction,
        // so there is nothing to host and nobody to wait for.
        if (GameContext.IsCli)
        {
            CliBootstrap.Start(this);
            return;
        }

        // Go straight to the multiplayer lobby for: a dedicated server (auto-hosts there),
        // an auto-joining test client, or the F6 debug flow. Everything else opens the main menu.
        bool toLobby = GameContext.IsDedicatedServer
                       || userArgs.Contains("is_debug_multiplayer=true")
                       || userArgs.Contains("auto_join=true");

        string targetScene = toLobby
            ? "res://scenes/menu/MultiplayerLobby.tscn"
            : "res://scenes/menu/Menu.tscn";

        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, targetScene);
    }
}
