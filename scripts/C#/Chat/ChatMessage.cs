/// <summary>Who hears a chat message: every peer, or only the sender's allies.</summary>
public enum ChatChannel
{
    Global,
    Team
}

/// <summary>Something a player typed, or something the game told this peer.</summary>
public enum ChatMessageKind
{
    Player,
    Game
}

/// <summary>One delivered chat line, as this peer received it.</summary>
/// <param name="IsOwn">
/// Stamped by the host per recipient, rather than compared against the local peer id, because the
/// host's id for a client and the client's own transport id can disagree (see SessionIdentity).
/// </param>
/// <param name="SenderName">Null for <see cref="ChatMessageKind.Game"/> lines, which have no sender.</param>
public record ChatMessage(int SenderPeerId, string SenderName, ChatChannel Channel, string Text, bool IsOwn,
                          ChatMessageKind Kind = ChatMessageKind.Player)
{
    /// <summary>Local clock at arrival, not the sender's: peers' clocks and time zones differ.</summary>
    public System.DateTime Time { get; init; } = System.DateTime.Now;

    /// <summary>Optional colour for the line's text; null leaves it to the chat's default.</summary>
    public Godot.Color? Color { get; init; }
}
