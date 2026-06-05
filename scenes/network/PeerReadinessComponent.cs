using Godot;

/// <summary>
/// Reusable child-node component that replicates the _readyPeers pattern.
/// 
/// Usage:
///   1. Add a PeerReadinessComponent node as a child of any Node (or subclass).
///      The child must exist on every peer at the same relative path.
///   2. From the parent's _Ready(), call GetNode&lt;PeerReadinessComponent&gt;("PeerReadinessComponent").RegisterReady();
///   3. Connect to the AllPeersReady signal for post-init logic.
/// 
/// Works regardless of the parent's base class (Node, CharacterBody2D, etc.).
/// </summary>
public partial class PeerReadinessComponent : Node
{
    /// <summary>Fired on every peer once all peers have reported ready.</summary>
    [Signal] public delegate void AllPeersReadyEventHandler();

    private readonly System.Collections.Generic.HashSet<int> _readyPeers = new();

    /// <summary>
    /// Call this from the parent node's _Ready() on both server and client peers.
    /// </summary>
    public void RegisterReady()
    {
        if (Multiplayer.IsServer())
        {
            _readyPeers.Add(1);
            CheckAllReady();
        }
        else
        {
            RpcId(1, nameof(NotifyPeerReady));
        }
    }

    /// <summary>Client → server: this peer's node is ready.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyPeerReady()
    {
        if (!Multiplayer.IsServer()) return;
        int peerId = Multiplayer.GetRemoteSenderId();
        DebugUtilities.PrintPeerFinest($"[{GetParent()?.Name}] Peer {peerId} ready");
        _readyPeers.Add(peerId);
        CheckAllReady();
    }

    private void CheckAllReady()
    {
        int expected = Multiplayer.GetPeers().Length + 1;
        if (_readyPeers.Count >= expected)
        {
            DebugUtilities.PrintPeerFinest($"[{GetParent()?.Name}] All peers ready — broadcasting");            
            EmitSignal(SignalName.AllPeersReady);
        }
    }
}
