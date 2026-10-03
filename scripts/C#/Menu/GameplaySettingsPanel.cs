using Godot;

/// <summary>
/// The Gameplay tab of <see cref="SettingsDialog"/>: presentation speed, debug verbosity, whether
/// display-only modals dismiss themselves, and whether the hand-play fan shows the active table cards.
///
/// These three used to be loose <c>PanelContainer</c>s pinned to the top-left of
/// <c>user_interface.tscn</c> — permanent HUD clutter for settings that get changed once and then
/// left alone, and unreachable from the main menu. They live here now, alongside every other
/// setting, and there is exactly one place each is edited.
///
/// Applied live rather than staged behind an Apply button, as in <see cref="AudioSettingsPanel"/>:
/// each is a single cheap write, and none of them can leave the player unable to reach the control
/// that would undo it — which is the whole reason the Video tab stages instead.
/// </summary>
public partial class GameplaySettingsPanel : VBoxContainer
{
    private OptionButton _speed;
    private OptionButton _debugLevel;
    private CheckButton  _autoDismiss;
    private CheckButton  _activeCards;
    private OptionButton _chatHide;

    public override void _Ready() => Guard.Try(ReadyInternal, "GameplaySettingsPanel._Ready");

    private void ReadyInternal()
    {
        _speed       = GetNode<OptionButton>("%SpeedOption");
        _debugLevel  = GetNode<OptionButton>("%DebugLevelOption");
        _autoDismiss = GetNode<CheckButton>("%AutoDismissToggle");
        _activeCards = GetNode<CheckButton>("%ActiveCardsToggle");
        _chatHide    = GetNode<OptionButton>("%ChatHideOption");

        // The picker entries are authored in the scene, and their *ids* — not their row order — are
        // the enum values the handlers below read back. Same guard as VideoSettingsPanel applies to
        // %DisplayModeOption, for the same reason: a mismatch silently stores a different value than
        // the one the player picked.
        AssertIdsMatchOrder(_speed, "%SpeedOption");
        AssertIdsMatchOrder(_debugLevel, "%DebugLevelOption");

        GameSettings settings = GameSettings.Instance;
        _speed.Selected            = (int)settings.PresentationSpeed;
        _debugLevel.Selected       = (int)settings.DebugLevel;
        _autoDismiss.ButtonPressed = settings.AutoDismissModal;
        _activeCards.ButtonPressed = settings.ShowActiveCardsInFan;
        // Ids are the seconds themselves (0, 5, 10), so this one is looked up by id, not by row.
        _chatHide.Selected         = _chatHide.GetItemIndex(settings.ChatHideSeconds);

        // Wired after seeding: assigning Selected/ButtonPressed would otherwise write the value
        // straight back out again on open.
        _speed.ItemSelected += index =>
            GameSettings.Instance.SetPresentationSpeed((GameSpeed)_speed.GetItemId((int)index));
        _debugLevel.ItemSelected += index =>
            GameSettings.Instance.SetDebugLevel((DebugVerbosity)_debugLevel.GetItemId((int)index));
        _chatHide.ItemSelected += index =>
            GameSettings.Instance.SetChatHideSeconds(_chatHide.GetItemId((int)index));
        _autoDismiss.Toggled += value =>
            GameSettings.Instance.SetAutoDismissModal(value);
        _activeCards.Toggled += value =>
        {
            GameSettings.Instance.SetShowActiveCardsInFan(value);
            // The fan is drawn behind this dialog, so a prompt already open has to be re-drawn or the
            // toggle looks dead until the next one. Skipped while browsing another faction's hand —
            // re-drawing there would take the display off the browse without ending it.
            if (!HandBrowsing.IsActive) InputManager.ShowCurrentCardPrompt();
        };
    }

    private static void AssertIdsMatchOrder(OptionButton option, string optionName)
    {
        for (int i = 0; i < option.ItemCount; i++)
        {
            if (option.GetItemId(i) == i) continue;
            GD.PushError($"GameplaySettingsPanel: {optionName} item ids do not match their enum values.");
            return;
        }
    }
}
