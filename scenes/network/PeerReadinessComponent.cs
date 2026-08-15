using Godot;
using System.Collections.Generic;

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

    private readonly HashSet<int> _readyPeers = new();

    /// <summary>
    /// A readiness barrier is one-shot. Latching it is what makes the disconnect recheck below safe:
    /// without this, rechecking after the barrier had already opened emitted AllPeersReady a second
    /// time, which re-ran GameManager.InitializeGame mid-game and cleared the player registry.
    /// </summary>
    private bool _hasFired = false;

    /// <summary>
    /// Every barrier currently alive on this peer, keyed by <see cref="BarrierId"/>.
    ///
    /// Static because a client's report no longer travels to THIS node's NodePath — it lands on the
    /// NetworkApi autoload, which then has to find the barrier by name. That indirection is the whole
    /// fix: /root/NetworkApi exists from the first frame of the process, whereas a barrier living in
    /// Game.tscn only exists once THIS peer has finished its own (independently timed) scene change.
    /// </summary>
    private static readonly Dictionary<string, PeerReadinessComponent> _live = new();

    /// <summary>
    /// Identity on the wire. Defaults to this node's absolute path, which is already required to be
    /// identical on every peer — that requirement is what made the old path-routed RPC work at all.
    /// Unlike a NodePath it does not have to EXIST on the receiver when the packet lands.
    /// </summary>
    public string BarrierId { get; set; }

    public override void _Ready()
    {
        BarrierId ??= GetPath().ToString();
        _live[BarrierId] = this;

        // `expected` is computed from the live peer list inside CheckAllReady, so a peer dropping
        // after others reported would otherwise leave the barrier permanently short — hanging
        // StartMultiplayerSession, or making the victory screen unreachable for everyone.
        if (Multiplayer != null)
            Multiplayer.PeerDisconnected += OnPeerDisconnected;

        // Reports that reached the host before this node existed. Draining them can never fire the
        // barrier by itself: `expected` counts this peer too, and it has not called RegisterReady yet.
        if (Multiplayer == null || !Multiplayer.IsServer()) return;

        foreach (int peerId in NetworkApi.TakeBufferedBarrierReports(BarrierId))
        {
            DebugUtilities.PrintPeerFinest($"[{GetParent()?.Name}] Draining parked ready report from peer {peerId}");
            _readyPeers.Add(peerId);
        }
        CheckAllReady();
    }

    public override void _ExitTree()
    {
        if (Multiplayer != null)
            Multiplayer.PeerDisconnected -= OnPeerDisconnected;

        // Only when the registry still points at us. change_scene_to_file frees the outgoing scene
        // before building the new one, so today the successor always registers after this runs — the
        // guard stops a future spawn order from letting a dying barrier unregister its live successor.
        if (BarrierId != null && _live.TryGetValue(BarrierId, out PeerReadinessComponent registered) && registered == this)
            _live.Remove(BarrierId);
    }

    /// <summary>
    /// Host-side delivery of a report that came in over <see cref="NetworkApi.NotifyBarrierReady"/>.
    /// Returns false when no barrier with this id exists on this peer yet — the caller's signal to
    /// park the report until one does.
    /// </summary>
    internal static bool Deliver(string barrierId, int peerId)
    {
        if (barrierId == null) return false;
        if (!_live.TryGetValue(barrierId, out PeerReadinessComponent barrier)) return false;

        // A freed barrier whose _ExitTree never ran is treated as absent, so its report is parked for
        // the replacement rather than thrown away.
        if (!IsInstanceValid(barrier))
        {
            _live.Remove(barrierId);
            return false;
        }

        DebugUtilities.PrintPeerFinest($"[{barrier.GetParent()?.Name}] Peer {peerId} ready");
        barrier._readyPeers.Add(peerId);
        barrier.CheckAllReady();
        return true;
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
            return;
        }

        // Via the NetworkApi autoload, never RpcId on this node — see the comment on _live and the
        // one above NetworkApi's readiness-barrier section.
        if (NetworkApi.Instance == null)
        {
            DebugUtilities.PrintPeerError(
                $"[{GetParent()?.Name}] No NetworkApi autoload — ready report for barrier {BarrierId} cannot be sent");
            return;
        }
        NetworkApi.Instance.ReportBarrierReady(BarrierId);
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
