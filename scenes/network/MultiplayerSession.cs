using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Spawned on both host and all clients via MultiplayerSpawner (spawn_path = Data in network.tscn).
/// Acts as the sole communication API between host and clients.
/// All RPCs for game state sync go through this class.
/// Authority is always the host (peer 1).
/// </summary>
public partial class MultiplayerSession : Node
{
    public static MultiplayerSession Instance { get; private set; }
    private PeerReadinessComponent _peerReadinessComponent => GetNode<PeerReadinessComponent>("PeerReadinessComponent");
    private PeerReadinessComponent _endGameReadiness => GetNode<PeerReadinessComponent>("EndGameReadinessComponent");

    public MultiplayerGameState GameState { get; private set; } = new();

    public override void _EnterTree()
    {
        base._EnterTree();
        Instance = this;
        DebugUtilities.PrintPeerFinest($"MultiplayerSession entered tree (IsServer={Multiplayer.IsServer()})");
    }

    public override void _Ready()
    {
        DebugUtilities.PrintPeerFinest($"MultiplayerSession ready on peer {Multiplayer.GetUniqueId()}");
        _endGameReadiness.AllPeersReady += OnAllPeersReadyForVictory; // fires on host only
        _peerReadinessComponent.RegisterReady();
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (HasNode("EndGameReadinessComponent"))
            _endGameReadiness.AllPeersReady -= OnAllPeersReadyForVictory;
        if (Instance == this)
            Instance = null;
    }

    public void StartNew(string configuration)
    {
        if (!Multiplayer.IsServer()) return;

        Rpc(nameof(StartSession), configuration);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public async void StartSession(string configuration)
    {
        // async void: an uncaught throw here goes to the synchronization context and kills the
        // process. A failure during session init is not recoverable (the readiness barrier will
        // never complete), but it must be visible rather than a silent crash.
        try
        {
            DebugUtilities.PrintPeerFinest($"LoadGame called with config: {configuration}");

            // Seed before any state is built. Here rather than at boot because a second game in the
            // same process must not inherit the first game's stream. An unseeded run still records
            // the seed it picked, so any session can be re-pinned afterwards.
            GameRandom.Initialize(CliArgs.GetInt("seed", System.Environment.TickCount));
            DebugUtilities.PrintPeer($"RNG seed: {GameRandom.Seed}");

            GameModeMultiplayerDefault gameMode = new GameModeMultiplayerDefault();
            await gameMode.Init();
            DebugUtilities.PrintPeerFinest("Game mode initialization complete, emitting MultiplayerSessionReady");


            if(Multiplayer.IsServer())
            {
                await gameMode.InitStartingState();
                GameFlow.Instance.StartGame();
            }


            //NodeUtilities.Instance.PlayersNode.GetChildren().ToList().ForEach(playerScene => (playerScene as PlayerScene).FadeLoadingScreen());
            // Null on a dedicated/headless server (it controls no faction, so has no local PlayerScene).
            PlayerScene.Current?.FadeLoadingScreen();
            EventBus.Emit(EventBus.SignalName.GameSessionStarted);
        }
        catch (Exception e)
        {
            ErrorReporter.Report(e, "MultiplayerSession.StartSession");
        }
    }

    /// <summary>
    /// Phase A of the synchronized game-end transition. Broadcast by the host when a win
    /// condition is met. Every peer stashes the result, waits for its OWN change-event and
    /// animation pipelines to drain (so a slower client finishes the final turn's effects
    /// first), then reports ready. The host only proceeds once all peers have reported.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public async void BeginEndGame(string resultJson)
    {
        try
        {
            DebugUtilities.PrintPeer("BeginEndGame received — draining local queues before victory screen");
            VictoryScreen.PendingResult = JsonSerializer.Deserialize<GameResult>(resultJson);

            // Drain change events first (applying one may enqueue animations), then animations.
            // WhenDrained() replaces `if (!IsIdle) await ToSignal(QueueDrained)`, which was a
            // check-then-await race that hung forever if the queue drained in between.
            await ChangeEventQueue.Instance.WhenDrained();
            await AnimationQueue.Instance.Start();
        }
        catch (Exception e)
        {
            // Report but still report ready: a peer that fails to drain must not strand every other
            // peer on the readiness barrier and leave the victory screen unreachable for everyone.
            ErrorReporter.Report(e, "MultiplayerSession.BeginEndGame");
        }
        finally
        {
            _endGameReadiness.RegisterReady();
        }
    }

    /// <summary>Phase B: runs on the host once host + all clients have drained and reported ready.</summary>
    private void OnAllPeersReadyForVictory()
    {
        if (!Multiplayer.IsServer()) return;
        DebugUtilities.PrintPeer("All peers ready — switching everyone to the victory screen");
        Rpc(nameof(PerformVictorySwitch));
    }

    /// <summary>Final step: every peer switches to the victory screen at the same time.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void PerformVictorySwitch()
    {
        SceneFlow.ChangeScene(this, "res://scenes/menu/VictoryScreen.tscn");
    }
}

