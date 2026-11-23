using Godot;

/// <summary>
/// The Skip-style "Show my action" button right of the prompt bar. Draws its icon beside the
/// caption; when it shows and what a press does are <c>MiniMenu</c>'s, which owns hand browsing.
/// </summary>
public partial class ShowActionButton : MenuPanelButton
{
	private const float IconSize = 16f;
	private const float IconGap = 7f;

	private const int BlinkCount = 3;
	private const int BlinkInMilliseconds = 150;
	private const int BlinkOutMilliseconds = 250;

	[Export] public Texture2D CaptionIcon { get; set; }

	private Panel _flash;
	private Tween _blink;

	public override void _Ready()
	{
		base._Ready();
		ButtonText = "Show my action";
		Visible = false;
		AddIcon();
		AddFlash();
	}

	public override void _Notification(int what)
	{
		base._Notification(what);
		if (what == NotificationVisibilityChanged) Blink();
	}

	/// <summary>A brass overlay in the hover colours, under the caption, that the blink fades in and out.</summary>
	private void AddFlash()
	{
		_flash = new Panel { MouseFilter = MouseFilterEnum.Ignore, Modulate = Colors.Transparent };
		_flash.SetAnchorsPreset(LayoutPreset.FullRect);
		_flash.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.851f, 0.678f, 0.361f, 0.3f),
			BorderColor = new Color(0.851f, 0.678f, 0.361f, 1f),
			BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
			CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomRight = 3, CornerRadiusBottomLeft = 3,
		});
		AddChild(_flash);
		MoveChild(_flash, GetNode("ContentContainer").GetIndex());
	}

	private void Blink()
	{
		if (_flash == null) return;
		_blink?.Kill();
		_flash.Modulate = Colors.Transparent;
		if (!Visible) return;

		_blink = CreateTween();
		for (int i = 0; i < BlinkCount; i++)
		{
			_blink.TweenProperty(_flash, "modulate:a", 1f, GameSettings.GetFixedDurationSeconds(BlinkInMilliseconds));
			_blink.TweenProperty(_flash, "modulate:a", 0f, GameSettings.GetFixedDurationSeconds(BlinkOutMilliseconds));
		}
	}

	/// <summary>Icon and caption centred as one group: the icon shrinks to the content's left edge, the caption right-aligns.</summary>
	private void AddIcon()
	{
		MarginContainer content = GetNodeOrNull<MarginContainer>("ContentContainer");
		Label label = content?.GetNodeOrNull<Label>("Label");
		if (content == null || label == null || CaptionIcon == null) return;

		content.AddChild(new TextureRect
		{
			Texture = CaptionIcon,
			CustomMinimumSize = new Vector2(IconSize, IconSize),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
			Modulate = new Color(1, 1, 1, 0.9f),
		});
		label.HorizontalAlignment = HorizontalAlignment.Right;

		float group = IconSize + IconGap + label.GetMinimumSize().X;
		int side = Mathf.Max(4, Mathf.FloorToInt((Mathf.Max(Size.X, CustomMinimumSize.X) - group) / 2f));
		content.AddThemeConstantOverride("margin_left", side);
		content.AddThemeConstantOverride("margin_right", side);
	}
}
