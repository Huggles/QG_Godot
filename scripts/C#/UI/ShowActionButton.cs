using Godot;

/// <summary>
/// The Skip-style "Show my action" button right of the prompt bar. Draws its icon beside the
/// caption; when it shows and what a press does are <c>MiniMenu</c>'s, which owns hand browsing.
/// </summary>
public partial class ShowActionButton : MenuPanelButton
{
	private const float IconSize = 16f;
	private const float IconGap = 7f;

	[Export] public Texture2D CaptionIcon { get; set; }

	public override void _Ready()
	{
		base._Ready();
		ButtonText = "Show my action";
		Visible = false;
		AddIcon();
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
