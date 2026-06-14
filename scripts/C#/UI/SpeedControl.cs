using Godot;
using System;

/// <summary>
/// Top-left HUD button that cycles the presentation speed through Slow → Normal → Fast → Very Fast.
/// </summary>
public partial class SpeedControl : PanelContainer
{
    private static readonly GameSpeed[] Speeds =
    {
        GameSpeed.Slow,
        GameSpeed.Normal,
        GameSpeed.Fast,
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
        label.Text = "Speed:";
        hbox.AddChild(label);

        _cycleButton = new Button();
        _cycleButton.Pressed += OnCyclePressed;
        hbox.AddChild(_cycleButton);

        UpdateButton();
    }

    private void OnCyclePressed()
    {
        int current = Array.IndexOf(Speeds, GameSettings.Instance.PresentationSpeed);
        int next = (current + 1) % Speeds.Length;
        GameSettings.Instance.SetPresentationSpeed(Speeds[next]);
        UpdateButton();
    }

    private void UpdateButton()
    {
        _cycleButton.Text = GameSettings.Instance.PresentationSpeed switch
        {
            GameSpeed.Slow   => "Slow",
            GameSpeed.Normal => "Normal",
            GameSpeed.Fast   => "Fast",
            _                => "Normal"
        };
    }
}
