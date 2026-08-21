using Godot;
using System.Threading.Tasks;

/// <summary>
/// The settings screen, with a Video and an Audio tab.
///
/// The same dialog serves the main menu and a game in progress, which is the whole reason it is a
/// <see cref="MenuModal"/>: that base is a CanvasLayer above everything with
/// <c>ProcessMode.Always</c>, so it draws and responds over any scene. The game's own
/// <c>ModalStack</c> could not be used — it lives inside <c>user_interface.tscn</c> and does not
/// exist on the menu.
///
/// The tabs own their own behaviour (<see cref="VideoSettingsPanel"/>,
/// <see cref="AudioSettingsPanel"/>); this class is only the shell around them. Layout lives in
/// <c>res://scenes/menu/SettingsDialog.tscn</c>, an inherited scene of <see cref="MenuModal"/>'s
/// shell.
/// </summary>
public partial class SettingsDialog : MenuModal
{
    private static readonly PackedScene Scene =
        GD.Load<PackedScene>("res://scenes/menu/SettingsDialog.tscn");

    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Opens the dialog over <paramref name="parent"/> and completes when it closes. Always
    /// completes — _ExitTree resolves it — so a scene change cannot leave the caller awaiting forever.
    /// </summary>
    public static Task ShowAsync(Node parent)
    {
        SettingsDialog dialog = Scene.Instantiate<SettingsDialog>();
        parent.AddChild(dialog);
        return dialog._closed.Task;
    }

    // Scene wiring: a GetNode failure here means a broken .tscn, which is a real bug worth
    // surfacing rather than a silent console line.
    public override void _Ready()
    {
        base._Ready();
        Guard.Try(ReadyInternal, "SettingsDialog._Ready");
    }

    public override void _ExitTree() => _closed.TrySetResult();

    private void ReadyInternal()
    {
        GetNode<Button>("%CloseButton").Pressed += Cancel;
    }

    protected override void Cancel()
    {
        if (Resolved) return;
        Resolved = true;
        // The tabs persist on the way out (see AudioSettingsPanel._ExitTree), so closing is a normal
        // resolution rather than a discard.
        QueueFree();
    }
}
