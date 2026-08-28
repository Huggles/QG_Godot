using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Browses the saves in <c>user://saves/</c> and starts one.
///
/// Shaped like <see cref="SteamFriendLobbiesScreen"/>, which is the existing screen with the same job:
/// a scrollable list of code-built rows each carrying its own action button, a status line, and a back
/// button.
///
/// Loading does NOT go straight to the game. It hands the save to <see cref="MultiplayerLobby"/> the
/// same way hosting does, because player-to-faction ownership is runtime-only and deliberately not
/// authoritative in a save: people re-claim factions in the lobby before the board is restored, and
/// they may pick differently from last time. Nothing about the replay depends on who ends up where —
/// the host replays alone.
/// </summary>
public partial class LoadGameScreen : Control
{
    private const string LobbyScenePath = "res://scenes/menu/MultiplayerLobby.tscn";

    /// <summary>
    /// Menu.tscn, not MainMenu.tscn: the latter is the bare Control without the Camera2D and
    /// CanvasLayer wrapper, so it comes up unrendered. Same note GameMenuModal carries.
    /// </summary>
    private const string MenuScenePath = "res://scenes/menu/Menu.tscn";

    private const string ColorInfo  = "#bbbbbb";
    private const string ColorError = "#d98a8a";

    private VBoxContainer   _saveList;
    private RichTextLabel   _statusLabel;
    private MenuPanelButton _backButton;

    /// <summary>
    /// True while a load is in flight. Needed independently of <c>Disabled</c> because the row buttons
    /// are plain Buttons and the flow spans several awaits, during which nothing else disables them.
    /// </summary>
    private bool _busy;

    // Scene wiring: a GetNode failure here means a broken .tscn, which is a real bug worth surfacing
    // rather than a silent console line.
    public override void _Ready() => Guard.Try(ReadyInternal, "LoadGameScreen._Ready");

    private void ReadyInternal()
    {
        _saveList    = GetNode<VBoxContainer>("%SaveList");
        _statusLabel = GetNode<RichTextLabel>("%StatusLabel");
        _backButton  = GetNode<MenuPanelButton>("%BackButton");

        _backButton.ButtonText = "Back";
        _backButton.Pressed   += OnBackPressed;

        Refresh();
    }

    private void Refresh()
    {
        List<SaveGameMeta> saves = SaveGameService.ListSaves();
        RebuildRows(saves);

        SetStatus(saves.Count == 0
            ? "No saved games yet. Save from the in-game menu with Escape."
            : $"{saves.Count} saved game(s).", ColorInfo);
    }

    private void RebuildRows(List<SaveGameMeta> saves)
    {
        foreach (Node child in _saveList.GetChildren())
            child.QueueFree();

        foreach (SaveGameMeta save in saves)
            _saveList.AddChild(BuildRow(save));
    }

    /// <summary>One save row, styled like the friend-lobby rows.</summary>
    private PanelContainer BuildRow(SaveGameMeta save)
    {
        PanelContainer panel = new();
        StyleBoxFlat style = new()
        {
            BgColor                = new Color(0.15f, 0.15f, 0.15f, 0.55f),
            CornerRadiusTopLeft    = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft      = 10, ContentMarginRight = 10,
            ContentMarginTop       = 8,  ContentMarginBottom = 8,
        };
        panel.AddThemeStyleboxOverride("panel", style);

        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 10);
        panel.AddChild(row);

        Label nameLabel = MakeLabel(save.DisplayName ?? "(unnamed)", 22, Vector2.Zero);
        nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        nameLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        row.AddChild(nameLabel);

        row.AddChild(MakeLabel(FormatSavedAt(save.SavedAtIso), 20, new Vector2(200, 0)));

        // Plain Buttons, not MenuPanelButton: a row action wants a compact size, and these two need a
        // Disabled that genuinely blocks while another load is in flight.
        Button loadButton = new() { Text = "Load", CustomMinimumSize = new Vector2(120, 44) };
        loadButton.Pressed += () => OnLoadPressed(save);
        row.AddChild(loadButton);

        Button deleteButton = new() { Text = "Delete", CustomMinimumSize = new Vector2(120, 44) };
        deleteButton.Pressed += () => OnDeletePressed(save);
        row.AddChild(deleteButton);

        return panel;
    }

    private static Label MakeLabel(string text, int fontSize, Vector2 minSize)
    {
        Label label = new() { Text = text, CustomMinimumSize = minSize, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }

    /// <summary>The stored timestamp is UTC ISO-8601; show it in the player's own time.</summary>
    private static string FormatSavedAt(string savedAtIso)
    {
        if (string.IsNullOrEmpty(savedAtIso)) return "—";

        return DateTimeOffset.TryParse(savedAtIso, CultureInfo.InvariantCulture,
                                       DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                                       out DateTimeOffset parsed)
            ? parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture)
            : savedAtIso;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Load
    // ══════════════════════════════════════════════════════════════════════════

    private void OnLoadPressed(SaveGameMeta meta)
    {
        if (_busy) return;
        Guard.FireAndForget(() => LoadFlowAsync(meta), "LoadGameScreen.Load");
    }

    private async Task LoadFlowAsync(SaveGameMeta meta)
    {
        SetBusy(true);
        try
        {
            SaveGame save = SaveGameService.Load(meta.FilePath);
            if (save == null)
            {
                SetStatus("That save could not be read. See the log for details.", ColorError);
                return;
            }

            if (save.Version != SaveGame.CurrentVersion)
            {
                SetStatus($"That save was written by an incompatible version ({save.Version}).", ColorError);
                return;
            }

            if (string.IsNullOrEmpty(save.ScenarioJsonBase64))
            {
                SetStatus("That save carries no scenario and cannot be restored.", ColorError);
                return;
            }

            // Nothing else to validate. The scenario travels inside the save, so the whole class of
            // "the scenario this save needs is missing" failure does not exist.

            HostOptionsDialog.Result choice = await HostOptionsDialog.PromptAsync(this);
            if (!IsInstanceValid(this)) return;

            switch (choice.Mode)
            {
                case HostOptionsDialog.HostMode.Godot:
                    MainMenu.SetHostGodot();
                    HandOff(save);
                    break;

                case HostOptionsDialog.HostMode.Steam:
                    long lobbyId = await HostLaunch.CreateSteamLobbyAsync(this, choice, save.ScenarioTitle);
                    if (!IsInstanceValid(this) || lobbyId == 0) return; // the helper already explained
                    MainMenu.SetHostSteam(lobbyId, choice.MaxPlayers);
                    HandOff(save);
                    break;
            }
        }
        finally
        {
            if (IsInstanceValid(this)) SetBusy(false);
        }
    }

    /// <summary>
    /// Hand the save to the lobby.
    ///
    /// Order matters: <c>PendingSave</c> is set AFTER SetHostGodot/SetHostSteam. MultiplayerLobby's
    /// Ready calls MainMenu.ClearLobbyIntent on itself before it hosts, so anything cleared there would
    /// be destroyed on arrival — which is exactly why the save does not ride on the lobby intent.
    /// </summary>
    private void HandOff(SaveGame save)
    {
        GameManager.ArmRestore(save);

        DebugUtilities.PrintPeer($"LoadGameScreen: restoring '{save.DisplayName}' ({save.Events.Count} event(s))");
        SceneFlow.ChangeScene(this, LobbyScenePath);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Delete / leave
    // ══════════════════════════════════════════════════════════════════════════

    private void OnDeletePressed(SaveGameMeta meta)
    {
        if (_busy) return;
        Guard.FireAndForget(() => DeleteFlowAsync(meta), "LoadGameScreen.Delete");
    }

    private async Task DeleteFlowAsync(SaveGameMeta meta)
    {
        SetBusy(true);
        try
        {
            bool confirmed = await MenuNotice.ShowConfirmAsync(this, "Delete save?",
                meta.DisplayName ?? "(unnamed)", "Delete");

            if (!IsInstanceValid(this) || !confirmed) return;

            if (SaveGameService.Delete(meta.FilePath))
                Refresh();
            else
                SetStatus("That save could not be deleted. See the log for details.", ColorError);
        }
        finally
        {
            if (IsInstanceValid(this)) SetBusy(false);
        }
    }

    /// <summary>
    /// No <c>leaveSession</c>: this screen never created a peer, so there is no session to leave — and
    /// asking for one would needlessly tear down state the menu still wants.
    /// </summary>
    private void OnBackPressed() => SceneFlow.ChangeScene(this, MenuScenePath);

    // ══════════════════════════════════════════════════════════════════════════
    // Status
    // ══════════════════════════════════════════════════════════════════════════

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _backButton.Disabled = busy;

        foreach (Node rowNode in _saveList.GetChildren())
            foreach (Node child in rowNode.GetChild(0).GetChildren())
                if (child is Button button) button.Disabled = busy;
    }

    private void SetStatus(string message, string colorHex)
        => _statusLabel.Text = $"[color={colorHex}][font_size=22]{message}[/font_size][/color]";
}
