using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Host-side wait for the faction intro: GameFlow.StartGame holds the opening prompt until every peer
/// that was in the game when the intro started has reported it finished, has left, or the timeout runs out.
/// </summary>
public static class FactionIntroBarrier
{
    // Generous: six capitals take about 35 s. Only a peer that never reports ever waits this long.
    private const double TimeoutSeconds = 75;

    private static readonly HashSet<int> _expected = new();
    private static readonly HashSet<int> _done = new();
    private static bool _begun;

    public static void Reset()
    {
        _expected.Clear();
        _done.Clear();
        _begun = false;
    }

    /// <summary>Transport peer ids that will play the intro, the host (1) included.</summary>
    public static void Begin(IEnumerable<int> peers)
    {
        _expected.UnionWith(peers);
        _begun = true;
    }

    public static void MarkDone(int peerId) => _done.Add(peerId);

    /// <summary>Returns when everyone is done; also gives up when the game it was started for has ended.</summary>
    public static async Task WaitAsync(Node node, int epoch)
    {
        SceneTree tree = node.GetTree();
        ulong start = Time.GetTicksMsec();
        while ((Time.GetTicksMsec() - start) / 1000.0 < TimeoutSeconds)
        {
            if (epoch != ErrorReporter.GameLoopEpoch || !GodotObject.IsInstanceValid(node)) return;
            if (_begun && AllDone(node)) return;
            await node.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        DebugUtilities.PrintPeerError($"Faction intro: gave up waiting on peers {string.Join(",", _expected.Except(_done))}");
    }

    // A peer that disconnected mid-intro no longer holds everyone else up.
    private static bool AllDone(Node node)
    {
        HashSet<int> connected = new(node.Multiplayer.GetPeers()) { 1 };
        return _expected.All(peer => _done.Contains(peer) || !connected.Contains(peer));
    }
}
