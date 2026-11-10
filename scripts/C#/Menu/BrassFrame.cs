using Godot;

/// <summary>
/// Animated brass trim (see BrassFrame.gdshader) laid over its parent, grown by <see cref="Inset"/>
/// so the brackets and glow can sit outside the parent's edge. On a button it can wake on hover.
/// </summary>
public partial class BrassFrame : ColorRect
{
	private static readonly Shader FrameShader = GD.Load<Shader>("res://scenes/menu/theme/BrassFrame.gdshader");

	[Export] public float Inset { get; set; } = 10f;
	[Export] public float BracketAlpha { get; set; } = 0.9f;
	[Export] public float BracketLength { get; set; } = 22f;
	[Export] public float SweepAlpha { get; set; } = 0.85f;
	[Export] public float SweepSpeed { get; set; } = 260f;
	[Export] public float SweepWidth { get; set; } = 90f;
	[Export] public float GlowAlpha { get; set; } = 0.18f;
	[Export] public float SheenAlpha { get; set; }
	[Export] public float SheenPeriod { get; set; } = 5f;

	/// <summary>Energy while the parent button is not hovered; 1 when it is. Ignored off a button.</summary>
	[Export] public float IdleEnergy { get; set; } = 1f;

	private ShaderMaterial _material;
	private BaseButton _button;
	private float _energy = -1f;
	private bool _dark;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		Color = Colors.White;
		_material = new ShaderMaterial { Shader = FrameShader };
		_material.SetShaderParameter("inset", Inset);
		_material.SetShaderParameter("bracket_alpha", BracketAlpha);
		_material.SetShaderParameter("bracket_len", BracketLength);
		_material.SetShaderParameter("sweep_alpha", SweepAlpha);
		_material.SetShaderParameter("sweep_speed", SweepSpeed);
		_material.SetShaderParameter("sweep_width", SweepWidth);
		_material.SetShaderParameter("glow_alpha", GlowAlpha);
		_material.SetShaderParameter("sheen_alpha", SheenAlpha);
		_material.SetShaderParameter("sheen_period", SheenPeriod);
		_material.SetShaderParameter("phase", GD.Randf() * 20f);
		Material = _material;

		_button = GetParent() as BaseButton;
		if (GetParent() is Control parent)
		{
			// A Container lays this out like any child; re-fit after it has.
			if (parent is Container container) container.SortChildren += FitDeferred;
			parent.Resized += FitDeferred;
		}
		FitDeferred();
	}

	private void FitDeferred() => CallDeferred(MethodName.Fit);

	private void Fit()
	{
		if (GetParent() is not Control parent) return;
		Position = new Vector2(-Inset, -Inset);
		Size = parent.Size + new Vector2(2 * Inset, 2 * Inset);
	}

	public override void _Process(double delta)
	{
		// A hover-only or disabled button shows nothing at rest, brackets included.
		bool dark = _button != null && (_button.Disabled || IdleEnergy <= 0f);
		if (dark != _dark || _energy < 0f)
		{
			_dark = dark;
			_material.SetShaderParameter("bracket_idle", dark ? 0f : 0.55f);
		}

		float target = _button == null ? 1f
			: _button.Disabled ? 0f
			: _button.IsHovered() || _button.HasFocus() ? 1f : IdleEnergy;
		float next = Mathf.MoveToward(Mathf.Max(_energy, 0f), target, (float)delta * 4f);
		if (Mathf.IsEqualApprox(next, _energy)) return;
		_energy = next;
		_material.SetShaderParameter("energy", _energy);
	}
}
