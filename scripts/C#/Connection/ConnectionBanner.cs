using Godot;
using System;

/// <summary>
/// The notice in the middle of the screen while a connection is lost: a large title ("Waiting for Bob
/// to reconnect"), a line of detail under it (the countdown), and an Options button for the host once
/// the 30 s are up. Built in code and parented to <see cref="ConnectionMonitor"/>, so it survives the
/// HUD being rebuilt.
/// </summary>
public partial class ConnectionBanner : CanvasLayer
{
    /// <summary>Above the HUD and its grey-out, below MenuModal (100), so the host's dialog still draws over it.</summary>
    private const int BannerLayer = 90;

    public event Action OptionsPressed;

    private Label _title;
    private Label _detail;
    private Button _options;

    public override void _Ready()
    {
        Layer = BannerLayer;

        CenterContainer centre = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(centre);

        PanelContainer panel = new() { CustomMinimumSize = new Vector2(520, 0) };
        panel.AddThemeStyleboxOverride("panel", GD.Load<StyleBox>("res://SimpleModalStyle.tres"));
        centre.AddChild(panel);

        MarginContainer margin = new();
        foreach (string side in new[] { "left", "right" }) margin.AddThemeConstantOverride($"margin_{side}", 32);
        foreach (string side in new[] { "top", "bottom" }) margin.AddThemeConstantOverride($"margin_{side}", 20);
        panel.AddChild(margin);

        VBoxContainer column = new() { Alignment = BoxContainer.AlignmentMode.Center };
        column.AddThemeConstantOverride("separation", 10);
        margin.AddChild(column);

        _title = NewLabel(32);
        _detail = NewLabel(20);
        _detail.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
        column.AddChild(_title);
        column.AddChild(_detail);

        _options = new Button
        {
            Text = "Options",
            Visible = false,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(160, 40),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        };
        _options.Pressed += () => OptionsPressed?.Invoke();
        column.AddChild(_options);
    }

    private static Label NewLabel(int fontSize)
    {
        Label label = new() { HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }

    public void Show(string title, string detail, bool offerOptions)
    {
        _title.Text = title;
        _detail.Text = detail;
        _detail.Visible = !string.IsNullOrEmpty(detail);
        _options.Visible = offerOptions;
        Visible = true;
    }
}
