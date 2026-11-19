using System;
using Godot;

/// <summary>
/// The lobby's chat panel: one channel, always shown, on the same <see cref="ChatService"/> as the
/// in-game chat. The lobby clears that history when the game starts, so nothing said here follows in.
/// </summary>
public partial class LobbyChat : PanelContainer
{
    private RichTextLabel Messages => GetNode<RichTextLabel>("%ChatMessages");
    private LineEdit Input => GetNode<LineEdit>("%ChatInput");

    public override void _Ready()
    {
        if (ChatService.Instance is not { } chat) return;

        Input.MaxLength = ChatService.MaxMessageLength;
        Input.KeepEditingOnTextSubmit = true;
        Input.TextSubmitted += OnTextSubmitted;

        // Lines can land before this panel exists (a client's first sync), so start from the history.
        foreach (ChatMessage message in chat.History) ChatBox.AppendLine(Messages, message);
        chat.MessageReceived += OnMessageReceived;
    }

    public override void _ExitTree()
    {
        if (ChatService.Instance != null) ChatService.Instance.MessageReceived -= OnMessageReceived;
    }

    private void OnTextSubmitted(string text)
    {
        // Enter on an empty line lets go of the input, as in game.
        if (string.IsNullOrWhiteSpace(text)) Input.ReleaseFocus();
        else ChatService.Instance?.Send(ChatChannel.Global, text);
        Input.Clear();
    }

    private void OnMessageReceived(ChatMessage message) => ChatBox.AppendLine(Messages, message);
}

/// <summary>Names senders by their lobby row while the lobby is up; there are no teams yet.</summary>
public sealed class LobbyChatRoster(Func<int, string> nameOf) : IChatRoster
{
    public string NameOf(int peerId) => nameOf(peerId);

    public bool AreTeammates(int sender, int recipient) => sender == recipient;

    public bool CanHear(int peerId) => true;
}
