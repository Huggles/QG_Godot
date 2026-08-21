using Godot;
using System.Threading.Tasks;

/// <summary>
/// The in-game Escape menu: Resume, Settings, Quit to Main Menu.
///
/// A <see cref="MenuModal"/> rather than a <c>PresentationModal</c> on the game's <c>ModalStack</c>:
/// that stack is for cards and input requests, it arranges its contents side by side against the
/// available width, and Escape there means "close the prompt". This is a plain overlay above all of
/// it, and it is the same base the settings dialog uses, so the two nest.
///
/// It deliberately does NOT pause the tree. Nothing in the project uses <c>SceneTree.Paused</c>, and
/// <see cref="ErrorPopup"/> documents why: the presentation flow is built on
/// <c>await ToSignal(tween, Finished)</c>, which a paused tree never delivers.
///
/// Opened by <see cref="GameMenuTrigger"/>, which owns the Escape arbitration against open prompts.
/// </summary>
public partial class GameMenuModal : MenuModal
{
    /// <summary>
    /// True while an instance is in the tree. Read by <see cref="GameMenuTrigger"/> so a second
    /// Escape cannot stack a second menu — relying on <c>SetInputAsHandled</c> ordering between two
    /// nodes that both implement <c>_UnhandledKeyInput</c> would be luck, not a guarantee.
    /// </summary>
    public static bool IsOpen { get; private set; }

    private static readonly PackedScene Scene =
        GD.Load<PackedScene>("res://scenes/menu/GameMenuModal.tscn");

    /// <summary>
    /// Guards the settings flow. Needed because <see cref="MenuPanelButton"/> emits Pressed even
    /// while Disabled, and because a second click during the await would open a second dialog.
    /// </summary>
    private bool _flowBusy;

    public static void Open(Node parent)
    {
        if (IsOpen || parent == null) return;
        parent.AddChild(Scene.Instantiate<GameMenuModal>());
    }

    // Scene wiring: a GetNode failure here means a broken .tscn, which is a real bug worth
    // surfacing rather than a silent console line.
    public override void _Ready()
    {
        base._Ready();
        IsOpen = true;
        Guard.Try(ReadyInternal, "GameMenuModal._Ready");
    }

    public override void _ExitTree() => IsOpen = false;

    private void ReadyInternal()
    {
        GetNode<MenuPanelButton>("%ResumeButton").Pressed     += Cancel;
        GetNode<MenuPanelButton>("%SettingsButton").Pressed   += OnSettingsPressed;
        GetNode<MenuPanelButton>("%QuitToMenuButton").Pressed += OnQuitToMenuPressed;
    }

    /// <summary>
    /// A CanvasLayer that is not <c>Visible</c> still runs input callbacks, so without this guard the
    /// menu parked behind the settings dialog would also eat that dialog's Escape — closing both at
    /// once, or worse, whichever the tree order happened to favour.
    /// </summary>
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!Visible) return;
        base._UnhandledKeyInput(@event);
    }

    /// <summary>
    /// Hidden rather than freed while the settings dialog is up, so Escape out of Settings lands back
    /// here instead of dropping the player straight into the game.
    /// </summary>
    private void OnSettingsPressed()
    {
        if (Resolved || _flowBusy) return;
        _flowBusy = true;
        Guard.FireAndForget(ShowSettingsAsync, "GameMenuModal.Settings");
    }

    private async Task ShowSettingsAsync()
    {
        try
        {
            Visible = false;
            await SettingsDialog.ShowAsync(GetTree().CurrentScene);
            if (!IsInstanceValid(this) || Resolved) return;
            Visible = true;
        }
        finally
        {
            if (IsInstanceValid(this)) _flowBusy = false;
        }
    }

    /// <summary>
    /// <c>Menu.tscn</c> rather than <c>MainMenu.tscn</c>: the latter is the bare Control without the
    /// Camera2D and CanvasLayer wrapper, so it comes up unrendered. <c>leaveSession: true</c> is what
    /// nulls the peer, clears buffered barrier reports and releases a Steam lobby — without it the
    /// next host or join attempt inherits the abandoned session.
    /// </summary>
    private void OnQuitToMenuPressed()
    {
        if (Resolved) return;
        Resolved = true;
        SceneFlow.ChangeScene(this, "res://scenes/menu/Menu.tscn", leaveSession: true);
    }

    protected override void Cancel()
    {
        if (Resolved) return;
        Resolved = true;
        QueueFree();
    }
}
