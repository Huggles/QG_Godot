using Godot;
using System.Collections.Generic;

/// <summary>
/// The Audio tab of <see cref="SettingsDialog"/>: the three bus levels and one picker per
/// configurable sound cue.
///
/// This used to be the sound panel in the game's bottom-left menu, which meant audio was only
/// reachable once a game had loaded. It lives here now so the main menu reaches the same controls,
/// and so there is exactly one place these values are edited.
///
/// Unlike the Video tab there is no Apply button: a volume you cannot hear while you set it is
/// useless, so levels apply live and persist themselves.
/// </summary>
public partial class AudioSettingsPanel : VBoxContainer
{
    private HSlider _masterVolume;
    private HSlider _musicVolume;
    private HSlider _sfxVolume;

    public override void _Ready() => Guard.Try(ReadyInternal, "AudioSettingsPanel._Ready");

    private void ReadyInternal()
    {
        _masterVolume = GetNode<HSlider>("%MasterVolumeSlider");
        _musicVolume  = GetNode<HSlider>("%MusicVolumeSlider");
        _sfxVolume    = GetNode<HSlider>("%SfxVolumeSlider");

        InitialiseVolumeSliders();
        BuildSfxSettingRows();
    }

    /// <summary>
    /// Catch-all persistence point, and the reason this is not left to <c>DragEnded</c> alone: a
    /// slider moved with the keyboard or the scroll wheel never emits it, so without this those
    /// changes would be lost when the dialog closes.
    /// </summary>
    public override void _ExitTree()
    {
        // Only when the sliders were actually wired — a Guard.Try failure above must not turn into a
        // second exception on the way out, and it would persist zeroes.
        if (_masterVolume == null) return;
        PersistVolumes();
    }

    /// <summary>
    /// Seeds the sliders from the saved levels and wires them so a drag is heard immediately while
    /// the config file is only rewritten once the drag ends: <c>GameSettings.Save</c> rewrites the
    /// whole file, so persisting on every value change would hit the disk every frame of a drag.
    /// </summary>
    private void InitialiseVolumeSliders()
    {
        GameSettings settings = GameSettings.Instance;
        _masterVolume.Value = settings.MasterVolume;
        _musicVolume.Value  = settings.MusicVolume;
        _sfxVolume.Value    = settings.SfxVolume;

        _masterVolume.ValueChanged += value => AudioManager.SetBusVolume(AudioManager.MasterBus, (float)value);
        _musicVolume.ValueChanged  += value => AudioManager.SetBusVolume(AudioManager.MusicBus,  (float)value);
        _sfxVolume.ValueChanged    += value => AudioManager.SetBusVolume(AudioManager.SfxBus,    (float)value);

        _masterVolume.DragEnded += _ => PersistVolumes();
        _musicVolume.DragEnded  += _ => PersistVolumes();
        _sfxVolume.DragEnded    += _ => PersistVolumes();
    }

    private void PersistVolumes()
    {
        GameSettings.Instance?.SetVolumes(
            (float)_masterVolume.Value,
            (float)_musicVolume.Value,
            (float)_sfxVolume.Value);
    }

    /// <summary>
    /// Builds one label + picklist per configurable cue in <c>assets/audio/sfx/settings.json</c>.
    /// Built in code rather than authored in the scene because which cues exist, and which sounds
    /// each offers, is data — a new entry in that file has to show up here without a scene edit.
    /// </summary>
    private void BuildSfxSettingRows()
    {
        AudioManager audio = AudioManager.Instance;
        if (audio == null)
        {
            return;
        }

        VBoxContainer container = GetNode<VBoxContainer>("%SfxSettingsContainer");

        foreach (SfxSettingData setting in audio.SfxSettings)
        {
            container.AddChild(new Label { Text = setting.Label ?? setting.Name });

            OptionButton picker = new OptionButton
            {
                Name = $"{setting.Name}Picker",
            };

            SfxSoundFileData selected = audio.SelectedOptionFor(setting.Name);
            for (int i = 0; i < setting.SoundFiles.Count; i++)
            {
                SfxSoundFileData option = setting.SoundFiles[i];
                picker.AddItem(option.Label ?? option.Name, i);
                if (option.Name == selected?.Name)
                {
                    picker.Selected = i;
                }
            }

            // Captured rather than read back off the sender: the signal carries only the item index,
            // and the button itself knows nothing about which setting it belongs to.
            string settingName = setting.Name;
            List<SfxSoundFileData> options = setting.SoundFiles;
            picker.ItemSelected += index => OnSfxOptionSelected(settingName, options, (int)index);

            container.AddChild(picker);
        }
    }

    /// <summary>
    /// Saves the picked sound and plays it once so the player hears what they just chose. Stored
    /// under the sound's <c>name</c>, never its label, so relabelling a sound in the data cannot
    /// orphan a saved choice.
    /// </summary>
    private void OnSfxOptionSelected(string settingName, List<SfxSoundFileData> options, int index)
    {
        if (index < 0 || index >= options.Count)
        {
            return;
        }

        GameSettings.Instance.SetSfxChoice(settingName, options[index].Name);

        // Through the setting rather than the clip: this plays whatever the choice just became, which
        // is exactly what the player should be hearing back.
        AudioManager.PlaySfxSetting(settingName);
    }
}
