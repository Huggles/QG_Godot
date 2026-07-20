using Godot;
using System.Collections.Generic;

public partial class MainMenu : Control
{
    public enum LobbyIntent { None, Host, Join }

    /// <summary>Set before navigating to the lobby so it can auto-connect.</summary>
    public static LobbyIntent PendingLobbyIntent { get; private set; } = LobbyIntent.None;
    public static void ClearLobbyIntent() => PendingLobbyIntent = LobbyIntent.None;

    public override void _Ready()
    {
        var singlePlayer     = GetNode<MenuPanelButton>("%SinglePlayerButton");
        var multiplayerHost  = GetNode<MenuPanelButton>("%MultiplayerHostButton");
        var multiplayerJoin  = GetNode<MenuPanelButton>("%MultiplayerJoinButton");
        var quit             = GetNode<MenuPanelButton>("%QuitButton");

        singlePlayer.ButtonText    = "Single Player";
        multiplayerHost.ButtonText = "Host Game";
        multiplayerJoin.ButtonText = "Join Game";
        quit.ButtonText            = "Quit";

        singlePlayer.Pressed    += OnSinglePlayerPressed;
        multiplayerHost.Pressed += OnMultiplayerHostPressed;
        multiplayerJoin.Pressed += OnMultiplayerJoinPressed;
        quit.Pressed            += OnQuitPressed;
    }

    private void OnSinglePlayerPressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/menu/GameModeSelectionScreen.tscn");
    }

    private void OnMultiplayerHostPressed()
    {
        PendingLobbyIntent = LobbyIntent.Host;
        GetTree().ChangeSceneToFile("res://scenes/menu/MultiplayerLobby.tscn");
    }

    private void OnMultiplayerJoinPressed()
    {
        PendingLobbyIntent = LobbyIntent.Join;
        GetTree().ChangeSceneToFile("res://scenes/menu/MultiplayerLobby.tscn");
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }
}
