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

    public static DebugVerbosity Debug => Instance.DebugLevel;
    public static bool IsDebugMultiplayer => Instance.DebugMultiplayer;

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
        => DurationTable[(int)Instance.PresentationSpeed, (int)scale];

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
    public void SetDebugMultiplayer(bool value)        { DebugMultiplayer  = value; Save(); }

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
            DebugMultiplayer  = config.GetValue(Section, "debug_multiplayer", false).As<bool>();
        }

        // Command-line user arg overrides the config file (e.g. launched via quick_launch.ps1)
        if (OS.GetCmdlineUserArgs().Contains("is_debug_multiplayer=true"))
            DebugMultiplayer = true;

        Save();
    }

    private void Save()
    {
        var config = new ConfigFile();
        config.SetValue(Section, "presentation_speed", (int)PresentationSpeed);
        config.SetValue(Section, "debug_level",         (int)DebugLevel);
        config.SetValue(Section, "debug_multiplayer",   DebugMultiplayer);
        config.Save(ConfigPath);
    }
}
