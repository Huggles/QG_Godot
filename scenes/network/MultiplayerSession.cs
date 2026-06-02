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
    
    public MultiplayerGameState GameState { get; private set; } = new();

    public override void _EnterTree()
    {
        base._EnterTree();
        Instance = this;
        DebugUtilities.PrintPeer($"MultiplayerSession entered tree (IsServer={Multiplayer.IsServer()})");
    }

    public override void _Ready()
    {
        DebugUtilities.PrintPeer($"MultiplayerSession ready on peer {Multiplayer.GetUniqueId()}");
        _peerReadinessComponent.RegisterReady();
    }

    public override void _ExitTree()
    {
        base._ExitTree();
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
        DebugUtilities.PrintPeer($"LoadGame called with config: {configuration}");
        GameModeMultiplayerDefault gameMode = new GameModeMultiplayerDefault();
        await gameMode.Init();
        DebugUtilities.PrintPeer("Game mode initialization complete, emitting MultiplayerSessionReady");
        

        if(Multiplayer.IsServer())
        {            
            await gameMode.InitStartingState();            
            GameFlow.Instance.StartGame();
        }


        //NodeUtilities.Instance.PlayersNode.GetChildren().ToList().ForEach(playerScene => (playerScene as PlayerScene).FadeLoadingScreen());
        PlayerScene.Current.FadeLoadingScreen();
        EventBus.Emit(EventBus.SignalName.GameSessionStarted);

    }
}

