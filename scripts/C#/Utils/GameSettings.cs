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

    public static DebugVerbosity Debug => Instance.DebugLevel;

    /// <summary>Duration in milliseconds for the current presentation speed.</summary>
    public static int Duration => Instance.PresentationSpeed switch
    {
        GameSpeed.Slow     => 3000,
        GameSpeed.Normal   => 1000,
        GameSpeed.Fast     => 500,
        GameSpeed.VeryFast => 250,
        _                  => 1000
    };

    
    public static int PauseDuration => Duration;
    public static double PauseDurationSeconds => Duration / 1000.0;
    public static int AnimationDuration => Duration;
    public static double AnimationDurationSeconds => AnimationDuration / 1000.0;

    public void SetPresentationSpeed(GameSpeed speed) { PresentationSpeed = speed; Save(); }
    public void SetDebugLevel(DebugVerbosity level)   { DebugLevel        = level; Save(); }

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
            PresentationSpeed = (GameSpeed)config.GetValue(Section, "presentation_speed", (int)GameSpeed.Normal).As<int>();
            DebugLevel        = (DebugVerbosity)config.GetValue(Section, "debug_level", (int)DebugVerbosity.INFO).As<int>();
        }
    }

    private void Save()
    {
        var config = new ConfigFile();
        config.SetValue(Section, "presentation_speed", (int)PresentationSpeed);
        config.SetValue(Section, "debug_level",         (int)DebugLevel);
        config.Save(ConfigPath);
    }
}
