using Godot;
using System;
using System.Collections.Generic;
using System.Linq;


/// <summary>
/// Application settings for presentation speed and debug verbosity.
/// Persisted via Godot's <see cref="ConfigFile"/> at <c>user://settings.cfg</c>.
/// Call <see cref="Load"/> once on startup to restore saved values.
/// </summary>
public partial class GameSettings : SingletonNode<GameSettings> 
{    
    private const string ConfigPath = "user://settings.cfg";
    private const string Section    = "gameplay";

    /// <summary>
    /// Section holding the chosen sound per configurable effect, keyed by the setting's name from
    /// <c>assets/audio/sfx/settings.json</c>. Its own section rather than mangled keys in
    /// <see cref="Section"/> because the set of settings is data-driven: entries come and go with
    /// that file, and <see cref="Save"/> has to be able to write back whatever it read.
    /// </summary>
    private const string SfxSection = "audio_sfx";

    /// <summary>Controls how fast animations play and how long pauses last.</summary>
    public GameSpeed PresentationSpeed { get; private set; } = GameSpeed.Normal;

    /// <summary>Controls how much debug information is printed to the console.</summary>
    public DebugVerbosity DebugLevel { get; private set; } = DebugVerbosity.INFO;

    /// <summary>When true, enables extra logging and tooling for multiplayer debugging.</summary>
    public bool DebugMultiplayer { get; private set; } = false;

    /// <summary>When true, display-only modals dismiss automatically after a delay instead of requiring the user to click Close.</summary>
    public bool AutoDismissModal { get; private set; } = true;

    /// <summary>
    /// When true, the hand-play prompt also draws the faction's active table cards — its played
    /// Status and Response cards — in the side fan, greyed out unless the host offered them.
    /// Presentation only; the faction row's Active Cards button keeps working either way.
    /// </summary>
    public bool ShowActiveCardsInFan { get; private set; } = true;

    /// <summary>Seconds the chat stays up after a player message or the mouse leaving; 0 never hides it.</summary>
    public int ChatHideSeconds { get; private set; } = 5;

    private static readonly int[] ChatHideChoices = { 0, 5, 10 };

    /// <summary>Last server address entered on the join screen, restored on the next launch.</summary>
    public string LastJoinIp { get; private set; } = "127.0.0.1";

    /// <summary>Last server port entered on the join screen, restored on the next launch.</summary>
    public int LastJoinPort { get; private set; } = MultiplayerLobby.DEFAULT_PORT;

    /// <summary>Overall output level, 0..1. Multiplies both of the levels below via the Master bus.</summary>
    public float MasterVolume { get; private set; } = 1.0f;

    /// <summary>Music level, 0..1, applied to the Music audio bus.</summary>
    public float MusicVolume { get; private set; } = 0.8f;

    /// <summary>Sound effect level, 0..1, applied to the SFX audio bus.</summary>
    public float SfxVolume { get; private set; } = 0.8f;

    /// <summary>How the window presents itself. Applied by <see cref="DisplaySettings"/>.</summary>
    public WindowDisplayMode DisplayMode { get; private set; } = DefaultDisplayMode;

    /// <summary>Fullscreen for players in a release export; windowed while developing.</summary>
    private static WindowDisplayMode DefaultDisplayMode => GameContext.IsProductionBuild
        ? WindowDisplayMode.BorderlessFullscreen
        : WindowDisplayMode.Windowed;

    /// <summary>
    /// Window size for <see cref="WindowDisplayMode.Windowed"/>. Both fullscreen modes run at the
    /// desktop resolution, so this is remembered rather than overwritten while one of those is active.
    ///
    /// Defaulted in <see cref="Load"/> from the screen (release) or the boot window (dev), rather
    /// than to a literal 1920x1080 that would push most of the window off a smaller monitor.
    /// </summary>
    public Vector2I WindowResolution { get; private set; } = new Vector2I(1920, 1080);

    /// <summary>
    /// Chosen sound per configurable effect: setting name → sound file name, both from
    /// <c>settings.json</c>. Only settings the player actually chose for appear here; anything
    /// missing falls back to the first sound the data offers (see <see cref="AudioManager"/>).
    /// </summary>
    private readonly Dictionary<string, string> _sfxChoices = new Dictionary<string, string>();

    public static DebugVerbosity Debug => Instance.DebugLevel;
    public static bool IsDebugMultiplayer => Instance.DebugMultiplayer;
    public static bool IsAutoDismissModal => Instance.AutoDismissModal;
    public static bool IsShowActiveCardsInFan => Instance.ShowActiveCardsInFan;
    public static bool IsDebugTeamsSwapped => OS.GetCmdlineUserArgs().Contains("swapped_teams=true");

    /// <summary>
    /// Duration table in milliseconds: rows = GameSpeed (Slow/Normal/Fast),
    /// columns = DurationScale (VeryLong/Long/Medium/Short/VeryShort).
    /// </summary>
    private static readonly int[,] DurationTable =
    {
        //  VeryLong  Long  Medium  Short  VeryShort
        {    6000,   4000,   3000,  2000,   1000 },   // Slow
        {    3000,   2000,   1000,   750,    500 },   // Normal
        {    1500,   1000,    500,   250,    100 },   // Fast
    };

    /// <summary>
    /// Duration in milliseconds for the current speed and the given scale.
    ///
    /// Zero while fast-forwarding a save-game restore, which is the single highest-leverage line in
    /// that feature: nearly all pacing in the project reads a GameSettings.Duration* property, so this
    /// collapses every tween and every Task.Delay pacing call at once, on the replaying host and on
    /// every client draining its broadcast burst.
    /// </summary>
    public static int GetDuration(DurationScale scale = DurationScale.Medium)
        => SkipPacing ? 0 : DurationTable[(int)Instance.PresentationSpeed, (int)scale];

    /// <summary>A fixed duration, in seconds, that ignores the speed setting but still skips with it.</summary>
    public static double GetFixedDurationSeconds(int milliseconds)
        => SkipPacing ? 0 : milliseconds / 1000.0;

    private static bool SkipPacing => GameContext.IsHeadless || ReplayContext.IsFastForwarding;

    /// <summary>Returns the duration in seconds for the current speed and the given scale.</summary>
    public static double GetDurationSeconds(DurationScale scale = DurationScale.Medium)
        => GetDuration(scale) / 1000.0;

    public static int    DurationVeryLong        => GetDuration(DurationScale.VeryLong);
    public static double DurationVeryLongSeconds => GetDurationSeconds(DurationScale.VeryLong);

    public static int    DurationLong            => GetDuration(DurationScale.Long);
    public static double DurationLongSeconds     => GetDurationSeconds(DurationScale.Long);

    public static int    DurationMedium          => GetDuration(DurationScale.Medium);
    public static double DurationMediumSeconds   => GetDurationSeconds(DurationScale.Medium);

    public static int    DurationShort           => GetDuration(DurationScale.Short);
    public static double DurationShortSeconds    => GetDurationSeconds(DurationScale.Short);

    public static int    DurationVeryShort        => GetDuration(DurationScale.VeryShort);
    public static double DurationVeryShortSeconds => GetDurationSeconds(DurationScale.VeryShort);

    public static bool ShowCountryLabels;
    public static bool ShowPlayersPanel;
    public static bool ShowDebugMenu;
    
    public void SetPresentationSpeed(GameSpeed speed) { PresentationSpeed = speed; Save(); }
    public void SetDebugLevel(DebugVerbosity level)   { DebugLevel        = level; Save(); }
    public void SetAutoDismissModal(bool value)        { AutoDismissModal  = value; Save(); }
    public void SetShowActiveCardsInFan(bool value)    { ShowActiveCardsInFan = value; Save(); }
    public void SetChatHideSeconds(int seconds)        { ChatHideSeconds = ValidChatHide(seconds); Save(); }

    /// <summary>A hand-edited settings.cfg must not leave the chat with a timer the picker cannot show.</summary>
    private static int ValidChatHide(int seconds) => System.Array.IndexOf(ChatHideChoices, seconds) >= 0 ? seconds : 5;
    public void SetLastJoinAddress(string ip, int port) { LastJoinIp = ip; LastJoinPort = port; Save(); }

    /// <summary>
    /// Persists all three audio levels in one write and pushes them onto the audio buses.
    /// Deliberately a single setter: <see cref="Save"/> rewrites the whole config file, so a
    /// slider must not call this per tick — see the sound panel in <c>MiniMenu</c>, which
    /// applies changes live through <see cref="AudioManager.SetBusVolume"/> and only lands here
    /// when the drag ends or the panel closes.
    /// </summary>
    public void SetVolumes(float master, float music, float sfx)
    {
        MasterVolume = Mathf.Clamp(master, 0f, 1f);
        MusicVolume  = Mathf.Clamp(music,  0f, 1f);
        SfxVolume    = Mathf.Clamp(sfx,    0f, 1f);
        Save();
        AudioManager.Instance?.ApplyVolumes();
    }

    /// <summary>
    /// Persists the window mode and size in one write and applies them. A single setter for the same
    /// reason as <see cref="SetVolumes"/>: <see cref="Save"/> rewrites the whole config file.
    /// </summary>
    public void SetDisplay(WindowDisplayMode mode, Vector2I resolution)
    {
        DisplayMode      = mode;
        WindowResolution = resolution;
        Save();
        DisplaySettings.Apply(mode, resolution);
    }

    /// <summary>
    /// The sound the player picked for <paramref name="settingName"/>, or null when they never
    /// picked one — the caller decides the default, because only the audio data knows what the
    /// options are.
    /// </summary>
    public string GetSfxChoice(string settingName)
        => _sfxChoices.TryGetValue(settingName, out string choice) ? choice : null;

    /// <summary>Records the sound chosen for a configurable effect and persists it.</summary>
    public void SetSfxChoice(string settingName, string soundFileName)
    {
        _sfxChoices[settingName] = soundFileName;
        Save();
    }

    public void SetShowCountryLabels(bool value) { ShowCountryLabels = value; Save(); }
    public void SetShowPlayersPanel(bool value) { ShowPlayersPanel = value; Save(); }
    public void SetShowDebugMenu(bool value) { ShowDebugMenu = value; Save(); }

    /// <summary>False until a display choice has been saved, so a first launch picks its own size.</summary>
    private bool _hasSavedDisplay;

    public override void _Ready()
    {
        base._Ready();
        GD.Print("GameSettings Ready");
        Load();

        // Same shape as AudioManager._Ready pushing the saved levels onto the buses: the values are
        // loaded here, so this is where they first reach the thing they describe. A dev build's
        // first launch skips it: its size was read off the window, so there is nothing to apply.
        // So does a window placed from the command line, e.g. the tiled multi-instance launch scripts.
        if ((_hasSavedDisplay || GameContext.IsProductionBuild) && !HasCmdlineWindowGeometry())
        {
            DisplaySettings.Apply(DisplayMode, WindowResolution);
        }
    }

    /// <summary>
    /// True for a multi-instance dev launch (<c>instance=N</c>), whose scripts size and place each
    /// window themselves, or any launch with an explicit window size or position.
    /// </summary>
    private static bool HasCmdlineWindowGeometry()
        => OS.GetCmdlineUserArgs().Any(arg => arg.StartsWith("instance="))
        || OS.GetCmdlineArgs().Any(arg => arg is "--resolution" or "--position");

    /// <summary>Loads settings from <c>user://settings.cfg</c>. Call once on startup.</summary>
    public void Load()
    {
        var config = new ConfigFile();
        if (config.Load(ConfigPath) == Error.Ok)
        {
            var saved = config.GetValue(Section, "presentation_speed", (int)GameSpeed.Normal).As<int>();
            // Clamp in case a saved VeryFast (3) value exists from before the redesign.
            PresentationSpeed = (GameSpeed)Math.Clamp(saved, 0, 2);
            DebugLevel        = (DebugVerbosity)config.GetValue(Section, "debug_level", (int)DebugVerbosity.INFO).As<int>();
            AutoDismissModal  = config.GetValue(Section, "auto_dismiss_modal", true).As<bool>();
            ShowActiveCardsInFan = config.GetValue(Section, "show_active_cards_in_fan", true).As<bool>();
            ChatHideSeconds   = ValidChatHide(config.GetValue(Section, "chat_hide_seconds", 5).As<int>());
            LastJoinIp        = config.GetValue(Section, "last_join_ip", "127.0.0.1").AsString();
            LastJoinPort      = config.GetValue(Section, "last_join_port", MultiplayerLobby.DEFAULT_PORT).As<int>();


            ShowCountryLabels = config.GetValue(Section, "show_country_labels", true).As<bool>();
            ShowPlayersPanel = config.GetValue(Section, "show_players_panel", true).As<bool>();
            ShowDebugMenu = config.GetValue(Section, "show_debug_menu", false).As<bool>();

            MasterVolume = Mathf.Clamp(config.GetValue(Section, "master_volume", 0.5f).As<float>(), 0f, 1f);
            MusicVolume  = Mathf.Clamp(config.GetValue(Section, "music_volume",  0.8f).As<float>(), 0f, 1f);
            SfxVolume    = Mathf.Clamp(config.GetValue(Section, "sfx_volume",    0.8f).As<float>(), 0f, 1f);

            DisplayMode = (WindowDisplayMode)Math.Clamp(
                config.GetValue(Section, "display_mode", (int)DefaultDisplayMode).As<int>(),
                0, (int)WindowDisplayMode.ExclusiveFullscreen);

            // Keyed off the width because that is what tells a saved choice apart from a first launch.
            _hasSavedDisplay = config.HasSectionKey(Section, "window_width");
            if (_hasSavedDisplay)
            {
                WindowResolution = new Vector2I(
                    config.GetValue(Section, "window_width",  1920).As<int>(),
                    config.GetValue(Section, "window_height", 1080).As<int>());
            }

            // Read by enumerating the section rather than by a fixed key list: which effects are
            // configurable is data, and a choice for a setting this build has never heard of has to
            // survive the round-trip rather than be dropped by Save().
            _sfxChoices.Clear();
            if (config.HasSection(SfxSection))
            {
                foreach (string settingName in config.GetSectionKeys(SfxSection))
                {
                    _sfxChoices[settingName] = config.GetValue(SfxSection, settingName, "").AsString();
                }
            }

        }

        // First launch, or a config file predating display settings: a release build picks from the
        // screen; while developing, adopt whatever size Godot opened at, already fitted to the monitor.
        if (!_hasSavedDisplay && !GameContext.IsHeadless)
        {
            WindowResolution = GameContext.IsProductionBuild
                ? DisplaySettings.DefaultResolution()
                : DisplayServer.WindowGetSize();
        }

        // DebugMultiplayer is a runtime-only flag, driven solely by the command-line arg used
        // by quick_launch.ps1 / F6. It is intentionally NOT loaded from or saved to the config
        // file: persisting it once caused every later launch (including from the main menu) to
        // inherit debug_multiplayer=true and auto-start the game as if F6 had been pressed.
        DebugMultiplayer = OS.GetCmdlineUserArgs().Contains("is_debug_multiplayer=true");

        Save();
    }

    private void Save()
    {
        var config = new ConfigFile();
        config.SetValue(Section, "presentation_speed", (int)PresentationSpeed);
        config.SetValue(Section, "debug_level",         (int)DebugLevel);
        // debug_multiplayer is deliberately not persisted — it is a runtime-only, command-line
        // driven flag (see Load). Persisting it would leak F6 auto-start into menu launches.
        config.SetValue(Section, "auto_dismiss_modal",    AutoDismissModal);
        config.SetValue(Section, "show_active_cards_in_fan", ShowActiveCardsInFan);
        config.SetValue(Section, "chat_hide_seconds",     ChatHideSeconds);
        config.SetValue(Section, "last_join_ip",          LastJoinIp);
        config.SetValue(Section, "last_join_port",        LastJoinPort);
        config.SetValue(Section, "show_country_labels",   ShowCountryLabels);
        config.SetValue(Section, "show_players_panel",    ShowPlayersPanel);
        config.SetValue(Section, "show_debug_menu",       ShowDebugMenu);
        config.SetValue(Section, "master_volume",         MasterVolume);
        config.SetValue(Section, "music_volume",          MusicVolume);
        config.SetValue(Section, "sfx_volume",            SfxVolume);
        config.SetValue(Section, "display_mode",          (int)DisplayMode);
        config.SetValue(Section, "window_width",          WindowResolution.X);
        config.SetValue(Section, "window_height",         WindowResolution.Y);

        foreach (KeyValuePair<string, string> choice in _sfxChoices)
        {
            config.SetValue(SfxSection, choice.Key, choice.Value);
        }

        config.Save(ConfigPath);
    }
}
