using Godot;
using System;


/// <summary>
/// Application settings for presentation speed and debug verbosity.
/// Persisted via Godot's <see cref="ConfigFile"/> at <c>user://settings.cfg</c>.
/// Call <see cref="Load"/> once on startup to restore saved values.
/// </summary>
public partial class GameSettings : SingletonNode<GameSettings> 
{    
    private const string ConfigPath = "user://settings.cfg";
    private const string Section    = "gameplay";

    /// <summary>Controls how fast animations play and how long pauses last.</summary>
    public GameSpeed PresentationSpeed { get; private set; } = GameSpeed.Normal;

    /// <summary>Controls how much debug information is printed to the console.</summary>
    public DebugVerbosity DebugLevel { get; private set; } = DebugVerbosity.INFO;

    /// <summary>When true, enables extra logging and tooling for multiplayer debugging.</summary>
    public bool DebugMultiplayer { get; private set; } = false;

    /// <summary>When true, display-only modals dismiss automatically after a delay instead of requiring the user to click Close.</summary>
    public bool AutoDismissModal { get; private set; } = true;

    /// <summary>Last server address entered on the join screen, restored on the next launch.</summary>
    public string LastJoinIp { get; private set; } = "127.0.0.1";

    /// <summary>Last server port entered on the join screen, restored on the next launch.</summary>
    public int LastJoinPort { get; private set; } = MultiplayerLobby.DEFAULT_PORT;

    public static DebugVerbosity Debug => Instance.DebugLevel;
    public static bool IsDebugMultiplayer => Instance.DebugMultiplayer;
    public static bool IsAutoDismissModal => Instance.AutoDismissModal;
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

    /// <summary>Returns the duration in milliseconds for the current speed and the given scale.</summary>
    public static int GetDuration(DurationScale scale = DurationScale.Medium)
        => GameContext.IsHeadless ? 0 : DurationTable[(int)Instance.PresentationSpeed, (int)scale];

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
    
    public void SetPresentationSpeed(GameSpeed speed) { PresentationSpeed = speed; Save(); }
    public void SetDebugLevel(DebugVerbosity level)   { DebugLevel        = level; Save(); }
    public void SetAutoDismissModal(bool value)        { AutoDismissModal  = value; Save(); }
    public void SetLastJoinAddress(string ip, int port) { LastJoinIp = ip; LastJoinPort = port; Save(); }

    public override void _Ready()
    {
        base._Ready();
        GD.Print("GameSettings Ready");
        Load();
    }

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
            LastJoinIp        = config.GetValue(Section, "last_join_ip", "127.0.0.1").AsString();
            LastJoinPort      = config.GetValue(Section, "last_join_port", MultiplayerLobby.DEFAULT_PORT).As<int>();
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
        config.SetValue(Section, "last_join_ip",          LastJoinIp);
        config.SetValue(Section, "last_join_port",        LastJoinPort);
        config.Save(ConfigPath);
    }
}
