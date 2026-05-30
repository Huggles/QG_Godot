using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

public partial class NetworkApi : Node
{
    
    public static NetworkApi Instance { get; private set; }
    

    [Signal] public delegate void MultiplayerSessionStartedEventHandler();
    [Signal] public delegate void PlayersLoadedEventHandler();

    public override void _Ready()
    {
        Instance = this;        
    }
    
    public async Task StartMultiplayerSession(string configuration)
    {
        if (!Multiplayer.IsServer()) return;

        var sessionStarted = ToSignal(this, SignalName.MultiplayerSessionStarted);
        Rpc(nameof(LoadMultiplayerSession));
        await sessionStarted;

        var playersLoaded = ToSignal(this, SignalName.PlayersLoaded);
        Rpc(nameof(LoadPlayers), configuration);
        await playersLoaded;
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void LoadMultiplayerSession()
    {
        MultiplayerSession multiplayerSessionInstance = AssetRepository.multiplayerSessionScenePacked.Instantiate<MultiplayerSession>();
        multiplayerSessionInstance.SetMultiplayerAuthority(1); // host is authority     
        if (Multiplayer.IsServer())
        {            
            PeerReadinessComponent peerReadinessComponent = multiplayerSessionInstance.GetNode<PeerReadinessComponent>("PeerReadinessComponent");
            peerReadinessComponent.AllPeersReady += ()=>{ EmitSignal(SignalName.MultiplayerSessionStarted); };
        }         
        AddChild(multiplayerSessionInstance, true);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void LoadPlayers(string configuration)
    {
        List<PlayerFactionAssignment> playerFactionAssignments = JsonSerializer.Deserialize<List<PlayerFactionAssignment>>(configuration);
        // Step 2: Create and register all players
        int remaining = playerFactionAssignments.Count;
        foreach (var assignment in playerFactionAssignments)
        {
            int peerId = assignment.PeerId;
            List<Faction> factions = assignment.Factions;
            
            string factionNames = string.Join(", ", factions);
            DebugUtilities.PrintPeer($"Creating player for peer {peerId} with factions: {factionNames}", DebugVerbosity.INFO);
            
            PlayerScene player = AssetRepository.PlayerScenePackged.Instantiate<PlayerScene>();
            player.SetMultiplayerAuthority(peerId);
            player.PlayerName = factions.Count == 1 
                ? $"{factions[0]} Player" 
                : $"Player {peerId}";

            if (Multiplayer.IsServer())
            {
                PeerReadinessComponent peerReadinessComponent = player.GetNode<PeerReadinessComponent>("PeerReadinessComponent");
                peerReadinessComponent.AllPeersReady += () => { if (--remaining == 0) EmitSignal(SignalName.PlayersLoaded); };
            }

            NodeUtilities.Instance.PlayersNode.AddChild(player);

            PlayerFactionRegistry.RegisterPlayer(player);
            
            DebugUtilities.PrintPeer($"Player {peerId} added to scene tree");
        }
        
        // Step 3: Assign factions to players
        foreach (var assignment in playerFactionAssignments)
        {
            int peerId = assignment.PeerId;
            List<Faction> factions = assignment.Factions;
            
            DebugUtilities.PrintPeer($"Assigning {factions.Count} faction(s) to peer {peerId}");
            PlayerFactionRegistry.AssignFactionsToPlayer(peerId, factions);
        }
    }

    /// <summary>
    /// Called on all clients by the server after a ChangeEvent is applied.
    /// Clients reconstruct the event, apply it locally, then verify the state hash.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public async void ReceiveChangeEvent(string dtoJson, string expectedHash)
    {
        DebugUtilities.PrintPeer($"ReceiveChangeEvent: {dtoJson[..Math.Min(80, dtoJson.Length)]}", DebugVerbosity.INFO);
        ChangeEventDto dto = JsonSerializer.Deserialize<ChangeEventDto>(dtoJson);
        ChangeEvent ev     = ChangeEvent.FromDto(dto);
        await ev.ApplyChange();

        string actualHash = MultiplayerSession.Instance.GameState.ComputeHash();
        DebugUtilities.PrintPeer($"{actualHash}", DebugVerbosity.INFO);
        if (actualHash != expectedHash)
        {
            DebugUtilities.PrintPeerError($"Hash mismatch after {dto.GetType().Name}: expected {expectedHash}, got {actualHash}. Requesting resync.");
            RpcId(1, nameof(RequestResync));
        }
    }

    /// <summary>Client → server: request a full state snapshot due to hash mismatch.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestResync()
    {
        if (!Multiplayer.IsServer()) return;
        int requestingPeer = Multiplayer.GetRemoteSenderId();
        DebugUtilities.PrintPeer($"Resync requested by peer {requestingPeer}", DebugVerbosity.INFO);
        string snapshotJson = JsonSerializer.Serialize(MultiplayerSession.Instance.GameState.BuildSnapshot());
        RpcId(requestingPeer, nameof(ReceiveFullSnapshot), snapshotJson);
    }

    /// <summary>Server → client: delivers a full state snapshot in response to RequestResync.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveFullSnapshot(string snapshotJson)
    {
        DebugUtilities.PrintPeer("ReceiveFullSnapshot: applying resync", DebugVerbosity.INFO);
        MultiplayerGameStateSnapshot snapshot = JsonSerializer.Deserialize<MultiplayerGameStateSnapshot>(snapshotJson);
        MultiplayerSession.Instance.GameState.ApplySnapshot(snapshot);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveGameStateUpdate(string gameStateJson, string changeEventType)
    {
        DebugUtilities.PrintPeer($"ReceiveGameStateUpdate: {changeEventType}", DebugVerbosity.INFO);
        MultiplayerGameStateSnapshot snapshot = JsonSerializer.Deserialize<MultiplayerGameStateSnapshot>(gameStateJson);
        MultiplayerSession.Instance.GameState.ApplySnapshot(snapshot);
        EventBus.Emit(EventBus.SignalName.GameChangeEventAfter, changeEventType);
    }
}
