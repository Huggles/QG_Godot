using Godot;
using System;

/// <summary>
/// The strip at the top of the screen while a connection is lost: "Waiting for Bob to reconnect… 25s",
/// with an Options button for the host once the 30 s are up. Built in code and parented to
/// <see cref="ConnectionMonitor"/>, so it survives the HUD being rebuilt.
/// </summary>
public partial class ConnectionBanner : CanvasLayer
{
    /// <summary>Above the HUD, below MenuModal (100), so the host's dialog still draws over it.</summary>
    private const int BannerLayer = 90;

    public event Action OptionsPressed;

    private Label _label;
    private Button _options;

    public override void _Ready()
    {
        Layer = BannerLayer;

        Control root = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        PanelContainer panel = new()
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f,
            OffsetTop = 60,
            GrowHorizontal = Control.GrowDirection.Both,
        };
        panel.AddThemeStyleboxOverride("panel", GD.Load<StyleBox>("res://SimpleModalStyle.tres"));
        root.AddChild(panel);

        MarginContainer margin = new();
        foreach (string side in new[] { "left", "right" }) margin.AddThemeConstantOverride($"margin_{side}", 14);
        foreach (string side in new[] { "top", "bottom" }) margin.AddThemeConstantOverride($"margin_{side}", 8);
        panel.AddChild(margin);

        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 12);
        margin.AddChild(row);

        _label = new Label { VerticalAlignment = VerticalAlignment.Center };
        _label.AddThemeFontSizeOverride("font_size", 18);
        row.AddChild(_label);

        _options = new Button { Text = "Options", Visible = false, FocusMode = Control.FocusModeEnum.None };
        _options.Pressed += () => OptionsPressed?.Invoke();
        row.AddChild(_options);
    }

    public void Show(string text, bool offerOptions)
    {
        _label.Text = text;
        _options.Visible = offerOptions;
        Visible = true;
    }
}
