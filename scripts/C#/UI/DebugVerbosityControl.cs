using Godot;
using System;

/// <summary>
/// HUD button that cycles the debug verbosity through NONE → INFO → FINEST.
/// </summary>
public partial class DebugVerbosityControl : PanelContainer
{
    private static readonly DebugVerbosity[] Levels =
    {
        DebugVerbosity.INFO,
        DebugVerbosity.FINEST
    };

    private Button _cycleButton;

    public override void _Ready()
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        AddChild(margin);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 6);
        margin.AddChild(hbox);

        var label = new Label();
        label.Text = "Debug:";
        hbox.AddChild(label);

        _cycleButton = new Button();
        _cycleButton.Pressed += OnCyclePressed;
        hbox.AddChild(_cycleButton);

        UpdateButton();
    }

    private void OnCyclePressed()
    {
        int current = Array.IndexOf(Levels, GameSettings.Instance.DebugLevel);
        int next = (current + 1) % Levels.Length;
        GameSettings.Instance.SetDebugLevel(Levels[next]);
        UpdateButton();
    }

    private void UpdateButton()
    {
        _cycleButton.Text = GameSettings.Instance.DebugLevel switch
        {
            DebugVerbosity.INFO   => "Info",
            DebugVerbosity.FINEST => "Finest",
            _                     => "Info"
        };
    }
}
