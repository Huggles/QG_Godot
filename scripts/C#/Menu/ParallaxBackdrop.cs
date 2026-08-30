using Godot;
using System.Collections.Generic;

/// <summary>
/// Drives the layered menu backdrop, <c>res://scenes/menu/ParallaxBackdrop.tscn</c>, whose children are
/// the backdrop sprites ordered back to front (Background, MiddleGround, Foreground). Every menu screen
/// instances that scene in place of the flat background image it used to carry.
///
/// The phase comes from <see cref="Time.GetTicksMsec"/> rather than from time accumulated in this
/// instance, which is what lets each screen own its own copy without the drift restarting. Menu
/// navigation is a full <c>ChangeSceneToFile</c>, so the backdrop on the screen being left is freed and
/// the incoming one is built from scratch; reading a clock they all share means the new instance picks
/// the sweep up exactly where the old one was, instead of snapping back to centre on every navigation.
///
/// The depth cue comes from the layers drifting at different rates: a near layer sweeps further than a
/// far one over the same interval, which is the parallax the eye reads as distance. The path is two
/// sines whose frequencies are in an irrational ratio, so the layers never return to a shared starting
/// pose and the loop stays unnoticeable however long the menu is left open. Each layer also carries a
/// phase offset, so they are never all at the same point of their sweep - moving in lockstep reads as
/// one flat image sliding, not as depth.
///
/// The layers are <see cref="Sprite2D"/> and not TextureRect on purpose, and converting them back would
/// visibly break the effect: <c>gui/common/snap_controls_to_pixels</c> is on by default, so a Control's
/// position is rounded to a whole pixel every layout pass. This drift moves single-digit pixels per
/// second, which under that rounding is not slow smooth motion but a one-pixel lurch every few tenths of
/// a second, stalling completely at each end of the sweep where the motion is slowest. Node2D transforms
/// are not snapped (<c>rendering/2d/snap/snap_2d_transforms_to_pixel</c> defaults off), so the same drift
/// lands on subpixel positions and the bilinear filter carries it smoothly between them.
///
/// Sizing is therefore ours rather than a layout container's: each sprite is centred in the backdrop and
/// scaled to cover it, keeping the art's aspect where the TextureRect stretched it to the rect, times the
/// overscan its own sweep needs so a drifting layer cannot pull its trailing edge into frame. That
/// overscan is per layer - the
/// front layer moves most and so is scaled most, which incidentally reinforces the depth, since nearer
/// art reads as larger.
/// </summary>
[GlobalClass]
public partial class ParallaxBackdrop : Control
{
	/// <summary>
	/// How far, in pixels, the frontmost layer sweeps from centre. Layers behind it move a fraction of
	/// this, per <see cref="LayerStrengths"/>. Bigger values need more overscan, so the art is scaled
	/// up further and loses fidelity.
	/// </summary>
	[Export(PropertyHint.Range, "0,300,1")]
	public float DriftPixels { get; set; } = 48.0f;

	/// <summary>
	/// Seconds for the horizontal sweep to complete one full cycle. The vertical sweep runs at
	/// <see cref="VerticalFrequencyRatio"/> of this, and the two together are what makes the motion a
	/// wander rather than a slide. Long by design - the movement should be noticeable only in hindsight.
	/// </summary>
	[Export(PropertyHint.Range, "5,600,1")]
	public float DriftSeconds { get; set; } = 90.0f;

	/// <summary>
	/// The vertical sweep's frequency as a multiple of the horizontal one. Irrational on purpose: at a
	/// rational ratio the two sines share a period and the layers trace the same closed figure forever.
	/// The default is the reciprocal golden ratio, the value furthest from any low-order fraction.
	/// </summary>
	[Export(PropertyHint.Range, "0.1,3,0.001")]
	public float VerticalFrequencyRatio { get; set; } = 0.618034f;

	/// <summary>
	/// Vertical sweep as a fraction of the horizontal one. Under 1 because a wide screen has more room
	/// to hide horizontal travel, and because near-horizontal drift is what camera movement looks like.
	/// </summary>
	[Export(PropertyHint.Range, "0,2,0.01")]
	public float VerticalScale { get; set; } = 0.55f;

	/// <summary>
	/// Per-layer sweep multipliers, back to front, matched to the children by index. A layer past the
	/// end of the array falls back to an even ramp across the children, so adding a fourth image to the
	/// scene still animates without touching this.
	/// </summary>
	[Export]
	public float[] LayerStrengths { get; set; } = { 0.22f, 0.55f, 1.0f };

	/// <summary>
	/// How much the pointer pushes the layers, as a fraction of <see cref="DriftPixels"/>. Zero leaves
	/// the backdrop purely time-driven; raising it adds the head-tilt cue on top of the drift. The pull
	/// is clamped, so the overscan below can budget for it.
	/// </summary>
	[Export(PropertyHint.Range, "0,1,0.01")]
	public float MouseInfluence { get; set; } = 0.0f;

	/// <summary>Seconds for the pointer pull to catch up, so the layers ease rather than snap.</summary>
	[Export(PropertyHint.Range, "0.01,3,0.01")]
	public float MouseSmoothing { get; set; } = 0.6f;

	/// <summary>The backdrop sprites, back to front, in tree order.</summary>
	private readonly List<Sprite2D> _layers = new();

	private Vector2 _mousePull = Vector2.Zero;

	public override void _Ready()
	{
		foreach (Node child in GetChildren())
		{
			if (child is Sprite2D layer)
			{
				_layers.Add(layer);
			}
		}

		Resized += ApplyCover;
		ApplyCover();
	}

	public override void _Process(double delta)
	{
		if (_layers.Count == 0)
		{
			return;
		}

		// Both sines are driven off one phase so the frequency ratio stays exact, rather than from two
		// independently accumulated angles that would drift apart in float over a long menu session.
		// The clock is engine uptime, shared by every instance - see the note on continuity above.
		double seconds = Time.GetTicksMsec() / 1000.0;
		float phase = (float)(Mathf.Tau * seconds / Mathf.Max(DriftSeconds, 0.001f));

		UpdateMousePull((float)delta);

		Vector2 centre = Size * 0.5f;

		for (int index = 0; index < _layers.Count; index++)
		{
			float strength = StrengthFor(index);

			// The phase offset is per layer and deliberately not a neat fraction of Tau, so no two
			// layers reach the end of their sweep together.
			float layerPhase = phase + index * 0.7f;

			Vector2 drift = new(
				Mathf.Sin(layerPhase) * DriftPixels * strength,
				Mathf.Sin(layerPhase * VerticalFrequencyRatio) * DriftPixels * strength * VerticalScale
			);

			_layers[index].Position = centre + drift + _mousePull * DriftPixels * strength;
		}
	}

	/// <summary>
	/// Eases the pointer pull towards where the mouse sits relative to the centre of the backdrop,
	/// inverted so the layers lean away from the cursor - the direction a scene shifts when the viewer
	/// leans into it. The result stays within +/- <see cref="MouseInfluence"/>, which is what lets the
	/// overscan account for it.
	/// </summary>
	private void UpdateMousePull(float delta)
	{
		Vector2 target = Vector2.Zero;

		if (MouseInfluence > 0.0f && Size.X > 0.0f && Size.Y > 0.0f)
		{
			Vector2 fromCentre = GetLocalMousePosition() - Size * 0.5f;
			target = new Vector2(
				Mathf.Clamp(fromCentre.X / (Size.X * 0.5f), -1.0f, 1.0f),
				Mathf.Clamp(fromCentre.Y / (Size.Y * 0.5f), -1.0f, 1.0f)
			) * -MouseInfluence;
		}

		// Exponential ease, framed against delta so the catch-up rate does not change with frame rate.
		float weight = 1.0f - Mathf.Exp(-delta / Mathf.Max(MouseSmoothing, 0.001f));
		_mousePull = _mousePull.Lerp(target, weight);
	}

	/// <summary>
	/// Sizes each layer to cover the backdrop, plus just enough overscan that its own sweep cannot
	/// expose an edge. Re-run on resize, because both parts depend on the window: the cover factor
	/// directly, and the overscan because the same pixel sweep is a larger fraction of a smaller window.
	/// </summary>
	private void ApplyCover()
	{
		if (Size.X <= 0.0f || Size.Y <= 0.0f)
		{
			return;
		}

		for (int index = 0; index < _layers.Count; index++)
		{
			Sprite2D layer = _layers[index];
			Vector2 texture = layer.Texture?.GetSize() ?? Vector2.Zero;

			if (texture.X <= 0.0f || texture.Y <= 0.0f)
			{
				continue;
			}

			// Worst case is the time drift and the mouse pull at full extent in the same direction.
			float reach = DriftPixels * StrengthFor(index) * (1.0f + MouseInfluence);

			// The larger of the two ratios is the one that covers; the smaller axis then overflows,
			// which is what keeps the aspect intact instead of stretching the art to fit.
			float cover = Mathf.Max(Size.X / texture.X, Size.Y / texture.Y);
			float overscan = Mathf.Max(
				1.0f + 2.0f * reach / Size.X,
				1.0f + 2.0f * reach * VerticalScale / Size.Y
			);

			layer.Scale = Vector2.One * cover * overscan;
		}
	}

	/// <summary>
	/// The sweep multiplier for a layer: from <see cref="LayerStrengths"/> where one is authored, and
	/// otherwise from an even back-to-front ramp, so an unconfigured layer still reads as nearer than
	/// the one behind it.
	/// </summary>
	private float StrengthFor(int index)
	{
		if (LayerStrengths != null && index < LayerStrengths.Length)
		{
			return LayerStrengths[index];
		}

		return _layers.Count <= 1 ? 1.0f : (index + 1) / (float)_layers.Count;
	}
}
