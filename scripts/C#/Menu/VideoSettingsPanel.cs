using Godot;
using System.Collections.Generic;

/// <summary>
/// The Video tab of <see cref="SettingsDialog"/>: window mode and resolution.
///
/// Unlike the Audio tab these stage behind an Apply button rather than taking effect on selection.
/// Cycling through window modes live would throw the player through several resizes on the way to
/// the one they wanted, and a resolution applied on hover-select is how you end up unable to reach
/// the control that would undo it. Apply lives in this tab rather than the dialog footer so it
/// cannot be read as also governing the volume sliders.
/// </summary>
public partial class VideoSettingsPanel : VBoxContainer
{
    private OptionButton _displayMode;
    private OptionButton _resolution;
    private Button       _applyButton;

    /// <summary>What Apply would commit. Only <see cref="OnApplyPressed"/> writes it to settings.</summary>
    private WindowDisplayMode _pendingMode;
    private Vector2I          _pendingResolution;

    /// <summary>
    /// Backs the resolution picker: item id is the index into this list, so the sizes never have to
    /// be parsed back out of their own labels.
    /// </summary>
    private IReadOnlyList<Vector2I> _resolutions = new List<Vector2I>();

    public override void _Ready() => Guard.Try(ReadyInternal, "VideoSettingsPanel._Ready");

    private void ReadyInternal()
    {
        _displayMode = GetNode<OptionButton>("%DisplayModeOption");
        _resolution  = GetNode<OptionButton>("%ResolutionOption");
        _applyButton = GetNode<Button>("%ApplyButton");

        // The %DisplayModeOption entries are authored in the scene, and their *ids* — not their
        // order — are the WindowDisplayMode values OnApplyPressed reads back. Getting them wrong
        // would silently put the player into a different window mode than the one they picked, hence
        // the assertion rather than trust. Same guard as HostOptionsDialog does for %Privacy.
        for (int i = 0; i < _displayMode.ItemCount; i++)
        {
            if (_displayMode.GetItemId(i) == i) continue;
            GD.PushError("VideoSettingsPanel: %DisplayModeOption item ids do not match WindowDisplayMode.");
            break;
        }

        GameSettings settings = GameSettings.Instance;
        _pendingMode       = settings.DisplayMode;
        _pendingResolution = settings.WindowResolution;

        _displayMode.Selected = (int)_pendingMode;
        BuildResolutionOptions();

        _displayMode.ItemSelected += OnDisplayModeSelected;
        _resolution.ItemSelected  += OnResolutionSelected;
        _applyButton.Pressed      += OnApplyPressed;

        RefreshState();
    }

    private void BuildResolutionOptions()
    {
        _resolutions = DisplaySettings.AvailableResolutions(_pendingResolution);

        _resolution.Clear();
        for (int i = 0; i < _resolutions.Count; i++)
        {
            Vector2I size = _resolutions[i];
            _resolution.AddItem($"{size.X} x {size.Y}", i);
            if (size == _pendingResolution)
            {
                _resolution.Selected = i;
            }
        }
    }

    private void OnDisplayModeSelected(long index)
    {
        _pendingMode = (WindowDisplayMode)_displayMode.GetItemId((int)index);
        RefreshState();
    }

    private void OnResolutionSelected(long index)
    {
        int id = _resolution.GetItemId((int)index);
        if (id < 0 || id >= _resolutions.Count) return;
        _pendingResolution = _resolutions[id];
        RefreshState();
    }

    private void OnApplyPressed()
    {
        GameSettings.Instance.SetDisplay(_pendingMode, _pendingResolution);

        // The window may have moved to another monitor, or changed which sizes fit, so the picker is
        // rebuilt against the screen it is on now.
        BuildResolutionOptions();
        RefreshState();
    }

    /// <summary>
    /// Keeps Apply and the resolution picker honest about the staged state: Apply is only live when
    /// there is something to commit, and the resolution is unavailable in both fullscreen modes,
    /// which run at the desktop resolution — offering a choice there would be a lie.
    /// </summary>
    private void RefreshState()
    {
        GameSettings settings = GameSettings.Instance;

        _resolution.Disabled = _pendingMode != WindowDisplayMode.Windowed;
        _applyButton.Disabled =
            _pendingMode == settings.DisplayMode && _pendingResolution == settings.WindowResolution;
    }
}
