/// <summary>
/// Everything chat needs to know about the people in a session. Kept behind an interface so
/// <see cref="ChatService"/> has no idea what a faction or a team is.
/// Only the host's roster is ever consulted: it routes every message and names every sender.
/// </summary>
public interface IChatRoster
{
    string NameOf(int peerId);

    /// <summary>Whether <paramref name="recipient"/> should hear <paramref name="sender"/>'s team chat.</summary>
    bool AreTeammates(int sender, int recipient);
}

/// <summary>The roster before any game installs one: everyone is a stranger with a numbered name.</summary>
public sealed class DefaultChatRoster : IChatRoster
{
    public string NameOf(int peerId) => $"Player {peerId}";

    public bool AreTeammates(int sender, int recipient) => sender == recipient;
}
