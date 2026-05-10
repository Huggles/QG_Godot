using Godot;
using System;


/// <summary>
/// Application settings for animation and pause durations.
/// Two independent settings — <see cref="AnimationSpeed"/> and <see cref="PauseSpeed"/> —
/// are persisted via Godot's <see cref="ConfigFile"/> at <c>user://settings.cfg</c>.
/// Call <see cref="Load"/> once on startup to restore saved values.
/// </summary>
public partial class GameSettings : SingletonNode<GameSettings> 
{    
    private const string ConfigPath = "user://settings.cfg";
    private const string Section    = "gameplay";

    /// <summary>Controls how fast tween animations play.</summary>
    public GameSpeed AnimationSpeed { get; private set; } = GameSpeed.Normal;

    /// <summary>Controls the length of pauses between game events.</summary>
    public GameSpeed PauseSpeed { get; private set; } = GameSpeed.Normal;

    /// <summary>Controls how much debug information is printed to the console.</summary>
    public DebugVerbosity DebugLevel { get; private set; } = DebugVerbosity.INFO;

    public static DebugVerbosity Debug => Instance.DebugLevel;

    /// <summary>Duration in milliseconds for tween animations (fade, scale, etc.).</summary>
    public static int AnimationDuration => Instance.AnimationSpeed switch
    {
        GameSpeed.Slow   => 400,
        GameSpeed.Normal => 200,
        GameSpeed.Fast   => 100,
        _                => 200
    };

    /// <summary>Duration in seconds for tween animations (fade, scale, etc.).</summary>
    public static double AnimationDurationSeconds => AnimationDuration / 1000.0;

    /// <summary>Duration in milliseconds for action pauses (used with Task.Delay).</summary>
    public static int PauseDuration => Instance.PauseSpeed switch
    {
        GameSpeed.Slow   => 3000,
        GameSpeed.Normal => 2000,
        GameSpeed.Fast   => 700,
        _                => 2000
    };

    public void SetAnimationSpeed(GameSpeed speed)  { AnimationSpeed = speed;  Save(); }
    public void SetPauseSpeed(GameSpeed speed)      { PauseSpeed     = speed;  Save(); }
    public void SetDebugLevel(DebugVerbosity level) { DebugLevel     = level;  Save(); }

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
            AnimationSpeed = (GameSpeed)config.GetValue(Section, "animation_speed", (int)GameSpeed.Normal).As<int>();
            PauseSpeed     = (GameSpeed)config.GetValue(Section, "pause_speed",     (int)GameSpeed.Normal).As<int>();
            DebugLevel     = (DebugVerbosity)config.GetValue(Section, "debug_level", (int)DebugVerbosity.INFO).As<int>();
        }
    }

    private void Save()
    {
        var config = new ConfigFile();
        config.SetValue(Section, "animation_speed", (int)AnimationSpeed);
        config.SetValue(Section, "pause_speed",     (int)PauseSpeed);
        config.SetValue(Section, "debug_level",     (int)DebugLevel);
        config.Save(ConfigPath);
    }
}
