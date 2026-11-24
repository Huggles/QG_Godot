using Godot;

/// <summary>
/// The in-game chat panel. Stays out of the way: it shows while hovered or typed in, and for
/// <see cref="GameSettings.ChatHideSeconds"/> after a player message arrives or the mouse leaves, then
/// fades out; a setting of 0 keeps it up for good. Once faded it is hidden outright, so it never
/// swallows clicks meant for the board underneath.
/// </summary>
public partial class ChatBox : PanelContainer
{
    private const int MaxLines = 100;

    private static readonly Color TeamColor = new("#7fd67f");
    private static readonly Color OwnNameColor = new("#ffd27f");
    private static readonly Color OtherNameColor = new("#9fc5ff");
    private static readonly Color GameColor = new("#b8b8b8");
    private static readonly Color TimeColor = new("#8a8a8a");

    private RichTextLabel Messages => GetNode<RichTextLabel>("%Messages");
    private LineEdit MessageInput => GetNode<LineEdit>("%MessageInput");
    private Button ChannelButton => GetNode<Button>("%ChannelButton");
    private Button PlayerFilterButton => GetNode<Button>("%PlayerFilterButton");
    private Button GameFilterButton => GetNode<Button>("%GameFilterButton");

    private ChatChannel _channel = ChatChannel.Global;
    private bool _shown;
    private double _hideIn;
    private Tween _fade;
    private bool _swallowEscapeRelease;
    private int _topZIndex;
    private bool _underModals;

    public override void _Ready()
    {
        // Only the local player's HUD chats. Shown offline too: Send echoes locally there.
        if (!SessionIdentity.IsLocalAuthority(this) || ChatService.Instance is not { } chat)
        {
            QueueFree();
            return;
        }

        _topZIndex = ZIndex;
        _shown = NeverHides;
        Visible = NeverHides;
        Modulate = NeverHides ? Colors.White : Colors.Transparent;

        MessageInput.MaxLength = ChatService.MaxMessageLength;
        MessageInput.KeepEditingOnTextSubmit = true;
        MessageInput.TextSubmitted += OnTextSubmitted;
        MessageInput.GuiInput += OnMessageInputGuiInput;
        ChannelButton.Pressed += ToggleChannel;
        RefreshChannel();
        PlayerFilterButton.Toggled += _ => Rebuild();
        GameFilterButton.Toggled += _ => Rebuild();

        Rebuild();
        chat.MessageReceived += OnMessageReceived;
    }

    public override void _ExitTree()
    {
        if (ChatService.Instance != null)
            ChatService.Instance.MessageReceived -= OnMessageReceived;
    }

    /// <summary>Read live, so changing the setting mid-game applies at once.</summary>
    private static bool NeverHides => GameSettings.Instance?.ChatHideSeconds is null or 0;

    public override void _Process(double delta)
    {
        bool engaged = NeverHides || MessageInput.HasFocus() || GetGlobalRect().HasPoint(GetGlobalMousePosition());
        if (engaged) Reveal();
        else if (_shown && (_hideIn -= delta) <= 0) FadeOut();
        KeepBelowModals();
    }

    /// <summary>
    /// A tall modal (the opening discard) reaches into the chat's corner, and the modal must win there.
    /// Clicks follow tree order rather than z_index, so the chat moves in the tree as well as in z.
    /// </summary>
    private void KeepBelowModals()
    {
        ModalStack stack = ModalStack.Current;
        bool under = IsInstanceValid(stack) && stack.Visible && stack.GetParent() == GetParent();
        if (under == _underModals) return;

        _underModals = under;
        ZIndex = under ? stack.ZIndex - 1 : _topZIndex;
        GetParent().MoveChild(this, under ? stack.GetIndex() : -1);
    }

    public override void _Input(InputEvent @event)
    {
        // The Escape that closed the input must not go on to open the game menu or close a modal.
        if (_swallowEscapeRelease && @event is InputEventKey { Keycode: Key.Escape, Pressed: false })
        {
            _swallowEscapeRelease = false;
            GetViewport().SetInputAsHandled();
            return;
        }

        // Clicking the board does not take focus on its own, and a focused input keeps WASD from panning.
        if (@event is InputEventMouseButton { Pressed: true } click && MessageInput.HasFocus()
            && !GetGlobalRect().HasPoint(click.Position))
            MessageInput.ReleaseFocus();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode is not (Key.Enter or Key.KpEnter) || MessageInput.HasFocus()) return;

        Reveal();
        MessageInput.GrabFocus();
        GetViewport().SetInputAsHandled();
    }

    private void OnMessageInputGuiInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

        if (key.Keycode == Key.Tab)
        {
            ToggleChannel();
            MessageInput.AcceptEvent();
        }
        else if (key.Keycode == Key.Escape)
        {
            MessageInput.ReleaseFocus();
            _swallowEscapeRelease = true;
            MessageInput.AcceptEvent();
        }
    }

    private void OnTextSubmitted(string text)
    {
        // Sending keeps the input open for the next line; Enter on an empty line closes it.
        if (string.IsNullOrWhiteSpace(text)) MessageInput.ReleaseFocus();
        else ChatService.Instance?.Send(_channel, text);
        MessageInput.Clear();
        Reveal();
    }

    private void OnMessageReceived(ChatMessage message)
    {
        if (!IsShown(message)) return;
        AppendLine(Messages, message);
        // Game lines stream in all turn; only someone talking is worth popping the chat open for.
        if (message.Kind == ChatMessageKind.Player) Reveal();
    }

    private bool IsShown(ChatMessage message) => message.Kind == ChatMessageKind.Game
        ? GameFilterButton.ButtonPressed
        : PlayerFilterButton.ButtonPressed;

    /// <summary>Redraws the list from the service's history, so toggling a filter back on restores what it hid.</summary>
    private void Rebuild()
    {
        Messages.Clear();
        foreach (ChatMessage message in ChatService.Instance?.History ?? [])
            if (IsShown(message)) AppendLine(Messages, message);
    }

    private void ToggleChannel()
    {
        _channel = _channel == ChatChannel.Global ? ChatChannel.Team : ChatChannel.Global;
        RefreshChannel();
    }

    private void RefreshChannel()
    {
        bool team = _channel == ChatChannel.Team;
        ChannelButton.Text = team ? "HUD_CHAT_CHANNEL_TEAM" : "HUD_CHAT_CHANNEL_ALL";
        ChannelButton.AddThemeColorOverride("font_color", team ? TeamColor : Colors.White);
        MessageInput.PlaceholderText = team ? "HUD_CHAT_PLACEHOLDER_TEAM" : "HUD_CHAT_PLACEHOLDER_ALL";
    }

    /// <summary>One formatted line; the lobby chat shares it. AddText rather than BBCode, so nothing typed is parsed as markup.</summary>
    public static void AppendLine(RichTextLabel messages, ChatMessage message)
    {
        if (messages.GetParagraphCount() >= MaxLines) messages.RemoveParagraph(0);
        if (messages.GetParsedText().Length > 0) messages.Newline();

        messages.PushColor(TimeColor);
        messages.AddText($"[{message.Time:HH:mm}] ");
        messages.Pop();

        if (message.Kind == ChatMessageKind.Game)
        {
            messages.PushColor(message.Color ?? GameColor);
            messages.AddText(message.Text);
            messages.Pop();
            return;
        }

        if (message.Channel == ChatChannel.Team)
        {
            messages.PushColor(TeamColor);
            messages.AddText(TranslationServer.Translate("HUD_CHAT_TEAM_PREFIX"));
            messages.Pop();
        }
        messages.PushColor(message.IsOwn ? OwnNameColor : OtherNameColor);
        messages.AddText($"{message.SenderName}: ");
        messages.Pop();
        messages.AddText(message.Text);
    }

    private void Reveal()
    {
        _hideIn = GameSettings.Instance?.ChatHideSeconds ?? 0;
        if (_shown) return;

        _shown = true;
        Visible = true;
        _fade?.Kill();
        _fade = CreateTween();
        _fade.TweenProperty(this, "modulate:a", 1.0f, GameSettings.ShowHideSeconds);
    }

    private void FadeOut()
    {
        _shown = false;
        _fade?.Kill();
        _fade = CreateTween();
        _fade.TweenProperty(this, "modulate:a", 0.0f, GameSettings.ShowHideSeconds);
        _fade.TweenCallback(Callable.From(() => Visible = false));
    }
}
