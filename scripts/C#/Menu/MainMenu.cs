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
        var victoryTest      = GetNode<MenuPanelButton>("%VictoryScreenTestButton");
        var quit             = GetNode<MenuPanelButton>("%QuitButton");

        singlePlayer.ButtonText    = "Single Player";
        multiplayerHost.ButtonText = "Host Game";
        multiplayerJoin.ButtonText = "Join Game";
        victoryTest.ButtonText     = "Victory Screen (Test)";
        quit.ButtonText            = "Quit";

        singlePlayer.Pressed    += OnSinglePlayerPressed;
        multiplayerHost.Pressed += OnMultiplayerHostPressed;
        multiplayerJoin.Pressed += OnMultiplayerJoinPressed;
        victoryTest.Pressed     += OnVictoryScreenTestPressed;
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

    /// <summary>Debug: fabricate a GameResult and jump straight to the victory screen.</summary>
    private void OnVictoryScreenTestPressed()
    {
        VictoryScreen.PendingResult = CreateFakeGameResult();
        GetTree().ChangeSceneToFile("res://scenes/menu/VictoryScreen.tscn");
    }

    private static GameResult CreateFakeGameResult()
    {
        // Made-up per-faction, per-round scores over 20 rounds.
        var fakeScores = new Dictionary<Faction, int[]>
        {
            [Faction.GERMANY]        = new[] { 3, 2, 4, 1, 3, 5, 2, 4, 3, 2, 4, 3, 5, 2, 3, 4, 2, 3, 4, 3 },
            [Faction.JAPAN]          = new[] { 2, 3, 1, 4, 2, 3, 1, 2, 3, 2, 1, 3, 2, 4, 2, 1, 3, 2, 3, 2 },
            [Faction.ITALY]          = new[] { 1, 0, 2, 1, 3, 1, 2, 1, 2, 0, 1, 2, 1, 3, 2, 1, 0, 2, 1, 2 },
            [Faction.UNITED_KINGDOM] = new[] { 2, 3, 2, 4, 1, 3, 2, 3, 2, 4, 3, 2, 1, 3, 2, 4, 3, 2, 3, 2 },
            [Faction.SOVIET]         = new[] { 4, 2, 3, 2, 4, 3, 2, 4, 3, 2, 4, 3, 2, 3, 4, 2, 3, 4, 2, 3 },
            [Faction.UNITED_STATES]  = new[] { 1, 2, 3, 2, 3, 4, 2, 3, 2, 3, 4, 2, 3, 2, 3, 4, 2, 3, 2, 3 },
        };

        // Fallback display data for when no game has been loaded (StaticGameData is empty on this path).
        var fakeLabels = new Dictionary<Faction, string>
        {
            [Faction.GERMANY] = "Germany",       [Faction.JAPAN] = "Japan",           [Faction.ITALY] = "Italy",
            [Faction.UNITED_KINGDOM] = "United Kingdom", [Faction.SOVIET] = "Soviet Union", [Faction.UNITED_STATES] = "United States",
        };
        // Match the real faction colours from QGData_Factions_V2.json.
        var fakeColors = new Dictionary<Faction, string>
        {
            [Faction.GERMANY] = "#5f5f5e",        [Faction.JAPAN] = "#ccddee",         [Faction.ITALY] = "#c20070",
            [Faction.UNITED_KINGDOM] = "#b1b103", [Faction.SOVIET] = "#b60101",        [Faction.UNITED_STATES] = "#006d23",
        };

        var factions = new List<FactionResult>();
        int axisTotal = 0;
        int alliesTotal = 0;

        foreach (Faction faction in StaticGameData.PlayableFactions)
        {
            FactionTeam team = StaticGameData.FactionTeamForFaction(faction);
            int[] roundPoints = fakeScores[faction];

            var perRound = new List<RoundScore>();
            int total = 0;
            for (int i = 0; i < roundPoints.Length; i++)
            {
                perRound.Add(new RoundScore { Round = i + 1, Points = roundPoints[i] });
                total += roundPoints[i];
            }

            if (team == FactionTeam.AXIS) axisTotal += total;
            else if (team == FactionTeam.ALLIES) alliesTotal += total;

            factions.Add(new FactionResult
            {
                Faction = faction,
                Team = team,
                Total = total,
                PerRound = perRound,
                FactionData = StaticGameData.FactionDataMap.GetValueOrDefault(faction)
                    ?? new FactionData
                    {
                        Index = (int)faction,
                        UniqueName = faction.ToString(),
                        Label = fakeLabels[faction],
                        ColorString = fakeColors[faction]
                    }
            });
        }

        return new GameResult
        {
            WinningTeam = alliesTotal > axisTotal ? FactionTeam.ALLIES : FactionTeam.AXIS,
            IsDraw = false,
            AxisTotal = axisTotal,
            AlliesTotal = alliesTotal,
            FinalRound = 20,
            EndReason = "Test result",
            Factions = factions
        };
    }
}
