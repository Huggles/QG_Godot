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
    /// A readiness barrier is one-shot. Latching it is what makes the disconnect recheck below safe:
    /// without this, rechecking after the barrier had already opened emitted AllPeersReady a second
    /// time, which re-ran GameManager.InitializeGame mid-game and cleared the player registry.
    /// </summary>
    private bool _hasFired = false;

    public override void _Ready()
    {
        // `expected` is computed from the live peer list inside CheckAllReady, so a peer dropping
        // after others reported would otherwise leave the barrier permanently short — hanging
        // StartMultiplayerSession, or making the victory screen unreachable for everyone.
        if (Multiplayer != null)
            Multiplayer.PeerDisconnected += OnPeerDisconnected;
    }

    public override void _ExitTree()
    {
        if (Multiplayer != null)
            Multiplayer.PeerDisconnected -= OnPeerDisconnected;
    }

    private void OnPeerDisconnected(long peerId)
    {
        if (!Multiplayer.IsServer()) return;
        if (_hasFired) return;   // barrier already open; nothing to recheck
        _readyPeers.Remove((int)peerId);
        DebugUtilities.PrintPeer($"[{GetParent()?.Name}] Peer {peerId} disconnected — rechecking readiness");
        CheckAllReady();
    }

    /// <summary>
    /// Report the barrier satisfied regardless of who is missing. For the error path: a peer that
    /// failed during a readiness window will never report, and stranding every other peer forever is
    /// worse than proceeding.
    /// </summary>
    public void ForceReady()
    {
        if (!Multiplayer.IsServer()) return;
        if (_hasFired) return;
        DebugUtilities.PrintPeer($"[{GetParent()?.Name}] Forcing readiness barrier open");
        _hasFired = true;
        EmitSignal(SignalName.AllPeersReady);
    }

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
        if (_hasFired) return;

        int expected = Multiplayer.GetPeers().Length + 1;
        if (_readyPeers.Count >= expected)
        {
            DebugUtilities.PrintPeerFinest($"[{GetParent()?.Name}] All peers ready — broadcasting");
            _hasFired = true;
            EmitSignal(SignalName.AllPeersReady);
        }
    }
}
