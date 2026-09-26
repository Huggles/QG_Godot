using Godot;

/// <summary>
/// Which peer id THIS peer is, as the host understands it.
///
/// Normally that is just <c>Multiplayer.GetUniqueId()</c>, and on the host it always is. The seam
/// exists because the two can disagree: a Steam session torn down and re-established inside one
/// process has been observed handing the host the previous session's peer id for a client whose own
/// freshly-built peer generated a different one. Every "is this mine?" check in the game compares a
/// multiplayer authority against the local id, and the authorities all come from the host
/// (NetworkApi.LoadPlayers), so a disagreement means that client silently owns nothing: no usable
/// faction rows in the lobby, no hand, no prompts.
///
/// The host already decides the player list, the names and the faction assignments. This makes it
/// decide identity too, which is the only way a client can be right about itself when the transport
/// is not. Falls back to the transport whenever the host has not spoken, so the ENet path and the
/// host itself behave exactly as before.
/// </summary>
public static class SessionIdentity
{
    /// <summary>The id the host addresses us by; 0 when it has not told us (or we are the host).</summary>
    private static int _hostAssigned;

    /// <param name="node">
    /// Any node in the tree, for its <c>Multiplayer</c>. Optional so the static registries can ask
    /// too; without one the default MultiplayerAPI is resolved off the main loop.
    /// </param>
    public static int LocalPeerId(Node node = null)
    {
        if (_hostAssigned != 0) return _hostAssigned;
        if (node != null) return node.Multiplayer.GetUniqueId();
        return Engine.GetMainLoop() is SceneTree tree ? tree.GetMultiplayer().GetUniqueId() : 1;
    }

    /// <summary>
    /// The check almost every UI node wants: does this node belong to the local player. Reads the
    /// authority the host set against the id the host knows us by, so both sides of the comparison
    /// come from the same source.
    /// </summary>
    public static bool IsLocalAuthority(Node node)
        => node.GetMultiplayerAuthority() == LocalPeerId(node);

    /// <summary>
    /// Client-side: record the id the host addresses us by. A mismatch is reported loudly rather than
    /// papered over — it means the transport handed the two ends different ids, which is a bug below
    /// this layer, and without this line it only ever surfaced as a lobby full of greyed-out buttons.
    /// </summary>
    public static void AdoptHostAssignedId(int hostAssigned, int transportId)
    {
        if (hostAssigned == 0) return;

        if (hostAssigned != transportId)
            DebugUtilities.PrintPeerErrorRaw(
                $"SessionIdentity: the host addresses us as peer {hostAssigned} but our own peer " +
                $"reports {transportId}. Using the host's id. The transport handed the two ends " +
                "different ids — a previous session's peer identity probably survived into this one.");

        _hostAssigned = hostAssigned;
    }

    /// <summary>Back to the transport's answer. Called when a session ends; see SceneFlow.</summary>
    public static void Reset() => _hostAssigned = 0;
}
