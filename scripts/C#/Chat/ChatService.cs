using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Text chat between peers on a global and a team channel. Knows nothing about the game: names and
/// teams come from <see cref="Roster"/>.
///
/// Every message goes through the host, which takes the sender from the transport rather than from
/// the message, so a client can neither speak as someone else nor read another team's chat. An
/// autoload for the same reason NetworkApi is one: /root/ChatService exists on every peer from the
/// first frame, so an RPC addressed here always has somewhere to land.
/// </summary>
public partial class ChatService : Node
{
    public const int MaxMessageLength = 200;
    private const int MaxHistory = 200;
    private const int OfflinePeerId = 1;

    public static ChatService Instance { get; private set; }

    /// <summary>Static so a game can install its roster without caring about autoload order.</summary>
    public static IChatRoster Roster { get; set; } = new DefaultChatRoster();

    /// <summary>Raised on this peer for every message it receives, its own included.</summary>
    public event Action<ChatMessage> MessageReceived;

    private readonly List<ChatMessage> _history = new();
    public IReadOnlyList<ChatMessage> History => _history;

    /// <summary>Whether there is anyone to talk to: a live, non-offline multiplayer peer.</summary>
    public bool IsAvailable =>
        Multiplayer.MultiplayerPeer is { } peer
        && peer is not OfflineMultiplayerPeer
        && peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;

    public override void _Ready() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public void Send(ChatChannel channel, string text)
    {
        text = Sanitize(text);
        if (text.Length == 0) return;

        // Offline there is nobody to route to, but the sender still sees their own line.
        // Peer 1, not GetUniqueId(): with the peer nulled at session end that call errors.
        if (!IsAvailable) Deliver(new ChatMessage(OfflinePeerId, Roster.NameOf(OfflinePeerId), channel, text, true));
        else if (Multiplayer.IsServer()) Route(Multiplayer.GetUniqueId(), channel, text);
        else RpcId(1, MethodName.SubmitMessage, (int)channel, text);
    }

    /// <summary>
    /// A local-only line from the game itself; never networked, since every peer posts its own.
    /// Safe off the main thread, and a repeat of the line just before it is dropped.
    /// </summary>
    public void PostGameMessage(string text)
    {
        text = Sanitize(text, int.MaxValue);
        if (text.Length == 0) return;

        Callable.From(() =>
        {
            if (_history.Count > 0 && _history[^1] is { Kind: ChatMessageKind.Game } last && last.Text == text) return;
            Deliver(new ChatMessage(0, null, ChatChannel.Global, text, false, ChatMessageKind.Game));
        }).CallDeferred();
    }

    /// <summary>Forgets the conversation. Called when a session ends so it does not follow into the next.</summary>
    public void Clear() => _history.Clear();

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SubmitMessage(int channel, string text)
    {
        // Re-checked here: the client's own Send is not to be trusted.
        if (!Multiplayer.IsServer() || !Enum.IsDefined(typeof(ChatChannel), channel)) return;
        text = Sanitize(text);
        if (text.Length == 0) return;

        Route(Multiplayer.GetRemoteSenderId(), (ChatChannel)channel, text);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveMessage(int senderId, string senderName, int channel, string text, bool isOwn)
        => Deliver(new ChatMessage(senderId, senderName, (ChatChannel)channel, text, isOwn));

    /// <summary>Host only: fans one message out to everyone who should hear it, the host included.</summary>
    private void Route(int senderId, ChatChannel channel, string text)
    {
        string senderName = Roster.NameOf(senderId);
        int host = Multiplayer.GetUniqueId();

        foreach (int peer in Multiplayer.GetPeers().Append(host))
        {
            bool isOwn = peer == senderId;
            if (channel == ChatChannel.Team && !isOwn && !Roster.AreTeammates(senderId, peer)) continue;

            if (peer == host) Deliver(new ChatMessage(senderId, senderName, channel, text, isOwn));
            else RpcId(peer, MethodName.ReceiveMessage, senderId, senderName, (int)channel, text, isOwn);
        }
    }

    private void Deliver(ChatMessage message)
    {
        _history.Add(message);
        if (_history.Count > MaxHistory) _history.RemoveAt(0);
        MessageReceived?.Invoke(message);
    }

    /// <summary>One trimmed line of printable text, cut to <paramref name="maxLength"/>.</summary>
    private static string Sanitize(string text, int maxLength = MaxMessageLength)
    {
        if (string.IsNullOrEmpty(text)) return "";

        var builder = new StringBuilder(text.Length);
        foreach (char c in text)
            builder.Append(char.IsControl(c) ? ' ' : c);

        string clean = builder.ToString().Trim();
        return clean.Length > maxLength ? clean[..maxLength] : clean;
    }
}
