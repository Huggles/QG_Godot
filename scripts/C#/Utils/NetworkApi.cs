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
        PlayerFactionRegistry.Clear();
        List<PlayerFactionAssignment> playerFactionAssignments = JsonSerializer.Deserialize<List<PlayerFactionAssignment>>(configuration);
        // Step 2: Create and register all players
        int remaining = playerFactionAssignments.Count;
        foreach (var assignment in playerFactionAssignments)
        {
            int peerId = assignment.PeerId;
            List<Faction> factions = assignment.Factions;
            
            string factionNames = string.Join(", ", factions);
            DebugUtilities.PrintPeerFinest($"Creating player for peer {peerId} with factions: {factionNames}");
            
            PlayerScene player = AssetRepository.PlayerScenePackged.Instantiate<PlayerScene>();
            player.SetMultiplayerAuthority(peerId);
            player.PlayerName = $"Player_{peerId}";

            if (Multiplayer.IsServer())
            {
                PeerReadinessComponent peerReadinessComponent = player.GetNode<PeerReadinessComponent>("PeerReadinessComponent");
                peerReadinessComponent.AllPeersReady += () => { if (--remaining == 0) EmitSignal(SignalName.PlayersLoaded); };
            }

            NodeUtilities.Instance.PlayersNode.AddChild(player);

            PlayerFactionRegistry.RegisterPlayer(player);
            
            DebugUtilities.PrintPeerFinest($"Player {peerId} added to scene tree");
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
    public async void ReceiveChangeEvent(string dtoJson)
    {        
        ChangeEventDto dto = JsonSerializer.Deserialize<ChangeEventDto>(dtoJson);
        ChangeEvent ev     = ChangeEvent.FromDto(dto);
        DebugUtilities.PrintPeer($"[color={"blue"}]ReceiveChangeEvent ({ev.Id}): {dto.GetType().Name}");
        DebugUtilities.PrintPeerFinest($"{dtoJson}");
        ev.ChangeEventApplied += (id) => {
            string actualHash = MultiplayerSession.Instance.GameState.ComputeHash();            
            if (actualHash != ev.HashAfterApplication)
            {
                DebugUtilities.PrintPeerError($"///////////////////////////////////////////////////////////////////////////");
                DebugUtilities.PrintPeerError($"DESYNC DETECTED after applying {dto.GetType().Name}!");
                DebugUtilities.PrintPeerError($"Hash mismatch after {dto.GetType().Name}: expected {ev.HashAfterApplication}, got {actualHash}. Requesting resync.");            
                DebugUtilities.PrintPeerError($"EventId: {ev.Id}, LatestAppliedId: {ChangeEvent.LatestAppliedId}");
                DebugUtilities.PrintPeerError($"///////////////////////////////////////////////////////////////////////////");
                DebugUtilities.PrintPeerError($"{JsonSerializer.Serialize(MultiplayerSession.Instance.GameState)}");
                RpcId(1, nameof(RequestResync));
                return;
            }            
        };
        ChangeEventQueue.Instance.Enqueue(ev);
    }

    public async Task<InputRequest> SendInputRequest(InputRequest inputRequest)
    {   
        DebugUtilities.PrintPeer($"[color={"purple"}]SendInputRequest: {inputRequest.GetType().Name}");        
        string payload = inputRequest.ToJson();        
        DebugUtilities.PrintPeerFinest($"{payload}");
        Rpc(nameof(NetworkApi.ReceiveInputRequest), payload);        
        var response = await EventBus.Instance.ToSignal(EventBus.Instance, nameof(EventBus.SignalName.InputRequestResponseReceived));
        InputRequest responseDto = InputRequest.FromJson(response[0].AsString());
        return responseDto;
    }

    /// <summary>
    /// Called on all clients by the server after a ChangeEvent is applied.
    /// Clients reconstruct the event, apply it locally, then verify the state hash.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public async void ReceiveInputRequest(string dtoJson)
    {        
        InputRequest dto = InputRequest.FromJson(dtoJson);
        DebugUtilities.PrintPeer($"[color={"purple"}]ReceiveInputRequest:  {dto.GetType().Name} (For me: {dto.IsForCurrentPeer})");
        DebugUtilities.PrintPeerFinest($"{dtoJson}");
        if (!ChangeEventQueue.Instance.IsIdle)
            await ChangeEventQueue.Instance.ToSignal(ChangeEventQueue.Instance, ChangeEventQueue.SignalName.QueueDrained);
        await dto.Execute();

        if (dto.IsForCurrentPeer)
        {
            Rpc(nameof(ReceiveInputResponse), dto.ToJson());
        }
    }


    

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private async void ReceiveInputResponse(string dtoJson)
    {
        if(!Multiplayer.IsServer())
        {
            PlayerActionLabel.HideText();
            return;
        }
        GameFlow.Instance.CurrentInputRequest = InputRequest.FromJson(dtoJson);            
        EventBus.Emit(EventBus.SignalName.InputRequestResponseReceived, dtoJson);
    }

    /// <summary>
    /// Broadcasts a PlayerActionLabel message from the server to all peers.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ShowPlayerActionLabel(string text, int duration, int faction)
    {
        PlayerActionLabel.ShowText(text, duration, (Faction)faction);
    }

    /// <summary>Client → server: request a full state snapshot due to hash mismatch.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestResync()
    {
        if (!Multiplayer.IsServer()) return;
        int requestingPeer = Multiplayer.GetRemoteSenderId();
        DebugUtilities.PrintPeer($"Resync requested by peer {requestingPeer}");
        string dto = JsonSerializer.Serialize(MultiplayerSession.Instance.GameState);

        DebugUtilities.PrintPeerError($"///////////////////////////////////////////////////////////////////////////");
        DebugUtilities.PrintPeerError($"DESYNC DETECTED!");
        DebugUtilities.PrintPeerError($"///////////////////////////////////////////////////////////////////////////");
        DebugUtilities.PrintPeerError($"{JsonSerializer.Serialize(MultiplayerSession.Instance.GameState)}");
        
        
        //RpcId(requestingPeer, nameof(ReceiveFullSnapshot), dto);
    }

    /// <summary>Server → client: delivers a full state snapshot in response to RequestResync.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveFullSnapshot(string snapshotJson)
    {        
        MultiplayerGameStateSnapshot snapshot = JsonSerializer.Deserialize<MultiplayerGameStateSnapshot>(snapshotJson);
        MultiplayerSession.Instance.GameState.ApplySnapshot(snapshot);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveGameStateUpdate(string gameStateJson, string changeEventType)
    {
        DebugUtilities.PrintPeer($"ReceiveGameStateUpdate: {changeEventType}");
        MultiplayerGameStateSnapshot snapshot = JsonSerializer.Deserialize<MultiplayerGameStateSnapshot>(gameStateJson);
        MultiplayerSession.Instance.GameState.ApplySnapshot(snapshot);
        EventBus.Emit(EventBus.SignalName.GameChangeEventAfter, changeEventType);
    }
}
