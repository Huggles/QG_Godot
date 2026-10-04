using Godot;
using System.Threading.Tasks;

/// <summary>
/// The two questions a lost connection raises. The host, once a player has been unreachable for 30 s:
/// wait for them, save and quit, or hand their factions to a bot. A client, once the host is gone:
/// quit, its only way on — a client never saves a networked game.
///
/// Layout lives in <c>res://scenes/userinterface/Connection/ConnectionLostDialog.tscn</c>, an inherited
/// scene of <see cref="MenuModal"/>'s shell.
/// </summary>
public partial class ConnectionLostDialog : MenuModal
{
    public enum Choice { Wait, SaveAndQuit, Bot, Quit }

    private static readonly PackedScene Scene =
        GD.Load<PackedScene>("res://scenes/userinterface/Connection/ConnectionLostDialog.tscn");

    private readonly TaskCompletionSource<Choice> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private string _title;
    private string _message;
    private bool _hostLost;
    private bool _canSave;

    /// <summary>
    /// The host's choice. Escape (or the dialog going away) means Wait: doing nothing is never the
    /// destructive answer. <paramref name="dialog"/> lets the caller dismiss it if the player returns.
    /// </summary>
    public static Task<Choice> PromptHostAsync(Node parent, string names, bool canSave, out ConnectionLostDialog dialog)
    {
        dialog = Scene.Instantiate<ConnectionLostDialog>();
        dialog._title = $"{names} lost connection";
        dialog._message = canSave
            ? "The game is paused. Wait for them to reconnect, save the game and quit, or let a bot play their factions."
            : "The game is paused. Wait for them to reconnect, quit, or let a bot play their factions. " +
              "The game is in the middle of an action, so it cannot be saved right now.";
        dialog._canSave = canSave;
        parent.AddChild(dialog);
        return dialog._result.Task;
    }

    /// <summary>A client's notice that the host is gone. Completes when Quit is pressed.</summary>
    public static Task<Choice> PromptHostLostAsync(Node parent)
    {
        ConnectionLostDialog dialog = Scene.Instantiate<ConnectionLostDialog>();
        dialog._title = "The host has disconnected";
        dialog._message = "The game cannot continue without the host.";
        dialog._hostLost = true;
        parent.AddChild(dialog);
        return dialog._result.Task;
    }

    public override void _Ready()
    {
        base._Ready();
        Guard.Try(ReadyInternal, "ConnectionLostDialog._Ready");
    }

    public override void _ExitTree() => _result.TrySetResult(_hostLost ? Choice.Quit : Choice.Wait);

    private void ReadyInternal()
    {
        GetNode<Label>("%Title").Text = _title;
        GetNode<Label>("%MessageLabel").Text = _message;

        Button wait = GetNode<Button>("%WaitButton");
        Button save = GetNode<Button>("%SaveQuitButton");
        Button bot = GetNode<Button>("%BotButton");
        Button quit = GetNode<Button>("%QuitButton");

        wait.Visible = save.Visible = bot.Visible = !_hostLost;
        quit.Visible = _hostLost;
        save.Text = _canSave ? "Save & Quit" : "Quit";

        wait.Pressed += () => Resolve(Choice.Wait);
        save.Pressed += () => Resolve(Choice.SaveAndQuit);
        bot.Pressed += () => Resolve(Choice.Bot);
        quit.Pressed += () => Resolve(Choice.Quit);
    }

    /// <summary>Escape: Wait for the host, nothing for a client (it has to leave).</summary>
    protected override void Cancel()
    {
        if (!_hostLost) Resolve(Choice.Wait);
    }

    /// <summary>Close without a choice, because the question no longer stands (the player came back).</summary>
    public void Dismiss()
    {
        if (Resolved || !IsInstanceValid(this)) return;
        Resolve(Choice.Wait);
    }

    private void Resolve(Choice choice)
    {
        if (Resolved) return;
        Resolved = true;
        _result.TrySetResult(choice);
        QueueFree();
    }
}
