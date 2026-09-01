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
    /// Guards the settings flow: a second click during the await would open a second dialog.
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

        // Disabled with a reason rather than hidden. A menu whose item COUNT differs between host and
        // client reads as a broken build; this way the answer to "why can I not?" is in the tooltip.
        // Same shape HostOptionsDialog uses for its Steam button. Single player is covered for free —
        // OfflineMultiplayerPeer has unique id 1, so IsServer() is true.
        MenuPanelButton saveButton = GetNode<MenuPanelButton>("%SaveGameButton");
        saveButton.Pressed += OnSaveGamePressed;
        if (!Multiplayer.IsServer() || GameContext.IsDedicatedServer)
        {
            saveButton.Disabled    = true;
            saveButton.TooltipText = "Only the host can save the game. Ask them to save it.";
        }
        else if (GameFlow.Instance?.Program?.AllowsSaving == false)
        {
            // Same shape as above: disabled with the reason in the tooltip. Load-bearing rather than
            // cosmetic — OnSaveGamePressed's deferred branch would otherwise accept a save that can
            // never be honoured, since TryFlushDeferredSave short-circuits on the same flag.
            saveButton.Disabled    = true;
            saveButton.TooltipText = GameFlow.Instance.SaveBlockedReason;
        }
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
    /// <summary>
    /// Save the game, or arrange for it to be saved the moment it can be.
    ///
    /// The capture is the FIRST thing this does, synchronously, before the flow goes async. This menu
    /// deliberately does not pause the tree, so the turn loop keeps producing events while it is open —
    /// but it runs on this same thread, so a straight-line capture inside a button callback is atomic
    /// with respect to it. Await anything first and the log grows underneath you.
    /// </summary>
    private void OnSaveGamePressed()
    {
        if (Resolved || _flowBusy) return;
        if (!Multiplayer.IsServer()) return;

        _flowBusy = true;

        string displayName = AutoSaveName();

        // Mid-card and mid-reaction the game has nothing resumable to point at — the prompt is being
        // held by an await deep inside a card step, and no save format can describe that. Rather than
        // grey the button out for what, with the always-ask reaction rule, is a large share of the
        // moments a player reaches for this menu, take the request now and honour it at the next step
        // boundary. That is usually seconds away.
        bool immediate = GameFlow.Instance.CanSave;
        string path = immediate ? MultiplayerSession.Instance.CaptureSave(displayName) : null;

        if (!immediate)
            GameFlow.Instance.RequestDeferredSave(displayName);

        Guard.FireAndForget(() => ReportSaveAsync(displayName, immediate, path), "GameMenuModal.SaveGame");
    }

    /// <summary>
    /// The name a save gets. Not editable: there is no text-entry dialog in the menu family, and a name
    /// derived from the position is more useful in the load list than whatever a player would type.
    /// </summary>
    private static string AutoSaveName()
    {
        string scenario = GameManager.ActiveScenarioTitle ?? "Game";
        return $"{scenario} — Round {GameFlow.Instance.Round}, {GameFlow.Instance.CurrentFaction.Label()}";
    }

    private async Task ReportSaveAsync(string displayName, bool immediate, string path)
    {
        try
        {
            Visible = false;

            if (!immediate)
                await MenuNotice.ShowMessageAsync(GetTree().CurrentScene, "Save queued",
                    $"'{displayName}' will be saved as soon as the current action finishes.");
            else if (path != null)
                await MenuNotice.ShowMessageAsync(GetTree().CurrentScene, "Game saved", displayName);
            else
                await MenuNotice.ShowMessageAsync(GetTree().CurrentScene, "Could not save",
                    "The save file could not be written. See the log for details.");

            if (!IsInstanceValid(this) || Resolved) return;
            Visible = true;
        }
        finally
        {
            if (IsInstanceValid(this)) _flowBusy = false;
        }
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
