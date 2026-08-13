using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class MainMenu : Control
{
    private const string LobbyScenePath        = "res://scenes/menu/MultiplayerLobby.tscn";
    private const string JoinScenePath         = "res://scenes/menu/JoinGameScreen.tscn";
    private const string SteamLobbiesScenePath = "res://scenes/menu/SteamFriendLobbiesScreen.tscn";

    public enum LobbyIntent { None, HostGodot, HostSteam, JoinSteam }

    /// <summary>Set before navigating to the lobby so it can auto-connect.</summary>
    public static LobbyIntent PendingLobbyIntent { get; private set; } = LobbyIntent.None;

    /// <summary>The Steam lobby the lobby screen should host on, or has already joined. 0 when unused.</summary>
    public static long PendingSteamLobbyId { get; private set; }

    /// <summary>Player cap chosen in the host dialog; only meaningful for a Steam host.</summary>
    public static int PendingMaxPlayers { get; private set; } = 6;

    /// <summary>
    /// Lobby the player was invited to through the Steam overlay, picked up by the friends screen.
    /// 0 when there is no pending invite.
    /// </summary>
    public static long PendingInviteLobbyId { get; private set; }

    /// <summary>Reads and clears the pending invite, so it can only ever be acted on once.</summary>
    public static long ConsumePendingInvite()
    {
        long lobbyId = PendingInviteLobbyId;
        PendingInviteLobbyId = 0;
        return lobbyId;
    }

    public static void SetHostGodot()
    {
        PendingLobbyIntent  = LobbyIntent.HostGodot;
        PendingSteamLobbyId = 0;
    }

    public static void SetHostSteam(long lobbyId, int maxPlayers)
    {
        PendingLobbyIntent  = LobbyIntent.HostSteam;
        PendingSteamLobbyId = lobbyId;
        PendingMaxPlayers   = maxPlayers;
    }

    public static void SetJoinSteam(long lobbyId)
    {
        PendingLobbyIntent  = LobbyIntent.JoinSteam;
        PendingSteamLobbyId = lobbyId;
    }

    public static void ClearLobbyIntent()
    {
        PendingLobbyIntent  = LobbyIntent.None;
        PendingSteamLobbyId = 0;
    }

    /// <summary>
    /// Guards the async host/join flows. Needed because <see cref="MenuPanelButton"/> emits Pressed
    /// even while Disabled, and because a second click during the await would open a second dialog.
    /// </summary>
    private bool _flowBusy;

    // Scene wiring: a GetNode failure here means a broken .tscn, which is a real bug worth
    // surfacing rather than a silent console line.
    public override void _Ready() => Guard.Try(ReadyInternal, "MainMenu._Ready");

    private void ReadyInternal()
    {
        ErrorInjection.MaybeThrow(ErrorInjection.Site.MenuReady);

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

        if (SteamworksApi.Instance != null)
            SteamworksApi.Instance.JoinRequested += OnSteamJoinRequested;
    }

    public override void _ExitTree()
    {
        if (SteamworksApi.Instance != null)
            SteamworksApi.Instance.JoinRequested -= OnSteamJoinRequested;
    }

    /// <summary>
    /// The player accepted an invite or pressed "Join game" in the Steam overlay. Handled by handing
    /// the lobby to the friends screen, which owns the join-then-connect sequence.
    /// </summary>
    private void OnSteamJoinRequested(long lobbyId)
    {
        // An invite can arrive at any time, including mid-session. Joining then would tear down a
        // game in progress, so it is ignored unless we are idle on the menu.
        if (Multiplayer.MultiplayerPeer != null)
        {
            DebugUtilities.PrintPeer($"Ignoring Steam invite to {lobbyId}: already in a session");
            return;
        }

        PendingInviteLobbyId = lobbyId;
        ClearLobbyIntent();
        SceneFlow.ChangeScene(this, SteamLobbiesScenePath);
    }

    private void OnSinglePlayerPressed()
    {
        SceneFlow.ChangeScene(this, "res://scenes/menu/GameModeSelectionScreen.tscn");
    }

    private void OnMultiplayerHostPressed()
    {
        if (_flowBusy) return;
        _flowBusy = true;
        Guard.FireAndForget(HostFlowAsync, "MainMenu.HostFlow");
    }

    private async Task HostFlowAsync()
    {
        try
        {
            HostOptionsDialog.Result choice = await HostOptionsDialog.PromptAsync(this);
            if (!IsInstanceValid(this)) return;

            switch (choice.Mode)
            {
                case HostOptionsDialog.HostMode.Godot:
                    SetHostGodot();
                    SceneFlow.ChangeScene(this, LobbyScenePath);
                    break;

                case HostOptionsDialog.HostMode.Steam:
                    await StartSteamHostAsync(choice);
                    break;
            }
        }
        finally
        {
            if (IsInstanceValid(this)) _flowBusy = false;
        }
    }

    /// <summary>
    /// Creates the Steam lobby before navigating. Doing it here rather than in the lobby screen means
    /// a Steam failure leaves the player on the menu with the Godot option still one click away,
    /// instead of stranding them on a lobby screen that never finished hosting.
    /// </summary>
    private async Task StartSteamHostAsync(HostOptionsDialog.Result choice)
    {
        MenuNotice busy = MenuNotice.ShowBusy(this, "Creating Steam lobby...");

        long lobbyId = await SteamworksApi.Instance.CreateLobbyAsync(choice.Privacy, choice.MaxPlayers);

        if (!IsInstanceValid(this)) return;
        busy.Dismiss();

        if (lobbyId == 0)
        {
            await MenuNotice.ShowMessageAsync(this, "Could not create a Steam lobby",
                "Steam did not create the lobby. You can still host over Godot networking.");
            return;
        }

        // Stamped now so the lobby is already described correctly the moment a friend's list refreshes.
        SteamworksApi.Instance.PublishLobbyMetadata(
            GameManager.Instance?.SelectedScenario?.Title ?? string.Empty, choice.MaxPlayers);

        SetHostSteam(lobbyId, choice.MaxPlayers);
        SceneFlow.ChangeScene(this, LobbyScenePath);
    }

    private void OnMultiplayerJoinPressed()
    {
        if (_flowBusy) return;
        _flowBusy = true;
        Guard.FireAndForget(JoinFlowAsync, "MainMenu.JoinFlow");
    }

    private async Task JoinFlowAsync()
    {
        try
        {
            JoinOptionsDialog.JoinMode mode = await JoinOptionsDialog.PromptAsync(this);
            if (!IsInstanceValid(this)) return;

            // Both join paths establish the connection on their own screen and hand the live peer to
            // the lobby, which adopts it. No pending intent is needed for the direct-IP path.
            ClearLobbyIntent();

            switch (mode)
            {
                case JoinOptionsDialog.JoinMode.DirectIp:
                    SceneFlow.ChangeScene(this, JoinScenePath);
                    break;

                case JoinOptionsDialog.JoinMode.SteamFriends:
                    SceneFlow.ChangeScene(this, SteamLobbiesScenePath);
                    break;
            }
        }
        finally
        {
            if (IsInstanceValid(this)) _flowBusy = false;
        }
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }

    /// <summary>Debug: fabricate a GameResult and jump straight to the victory screen.</summary>
    private void OnVictoryScreenTestPressed()
    {
        VictoryScreen.PendingResult = CreateFakeGameResult();
        SceneFlow.ChangeScene(this, "res://scenes/menu/VictoryScreen.tscn");
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
