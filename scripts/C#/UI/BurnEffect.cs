using System.Collections.Generic;
using Godot;

/// <summary>
/// Burns a Control and everything under it away, driving Burn.gdshader.
///
/// A material only ever affects the node it sits on - never that node's children - so a card, being a
/// Panel with art, two labels and a button under it, cannot be burnt by one material on its root the way
/// a lone Sprite2D can. This hands the SAME material to every canvas item in the subtree and switches
/// the shader into group space, where each of them measures the burn in the root's coordinates instead
/// of its own. One shared material also means one uniform write reaches the whole card, which is what
/// keeps <see cref="Burn"/> a single number rather than a per-node broadcast.
///
/// Attach it, tween <see cref="Burn"/> from 0 to 1, then <see cref="Clear"/> (or free the card).
/// <code>
/// BurnEffect burn = BurnEffect.Attach(cardScene);
/// burn.Play(1.2f).Finished += () => cardScene.QueueFree();
/// </code>
/// </summary>
/// 

[GlobalClass]
public partial class BurnEffect : Node
{
	private static readonly Shader BurnShader = GD.Load<Shader>("res://assets/materials/shaders/Burn.gdshader");

	/// <summary>
	/// What each node had on it before, so <see cref="Clear"/> can put it back. A card that is being
	/// burnt as a preview rather than destroyed still wants its own materials afterwards, and the ones
	/// here are set from code, so nothing else would restore them.
	/// </summary>
	private readonly Dictionary<CanvasItem, Material> replacedMaterials = new();

	private Control target;
	private float burn;

	// Only pushed when it actually changes: a card in a hand sits still for most of its life, and the
	// burn is the one thing here that moves every frame on its own.
	private Transform2D lastGlobalTransform;
	private Vector2 lastSize;

	/// <summary>
	/// The one material shared by the whole subtree, for tuning the look - <c>ember_color</c>,
	/// <c>noise_scale_pixels</c>, <c>direction_degrees</c> and the rest. Never one shared resource
	/// across cards: the burn amount lives in here, so two cards on one material would burn as one.
	/// </summary>
	public ShaderMaterial Material { get; } = new ShaderMaterial { Shader = BurnShader };

	/// <summary>How far the card has burnt: 0 is untouched, 1 is gone.</summary>
	public float Burn
	{
		get => burn;
		set
		{
			burn = Mathf.Clamp(value, 0.0f, 1.0f);
			Material.SetShaderParameter("burn", burn);
		}
	}

	/// <summary>
	/// Starts burning <paramref name="target"/>, from the node itself down. Adds itself as a child so it
	/// dies with the card and so a card freed mid-burn needs no cleanup.
	/// </summary>
	public static BurnEffect Attach(Control target)
	{
		BurnEffect effect = new BurnEffect { Name = nameof(BurnEffect) };
		target.AddChild(effect);
		return effect;
	}

	public override void _Ready()
	{
		// GetParent rather than a field, so the node also works dropped into a scene from the editor.
		target = GetParent<Control>();

		Material.SetShaderParameter("group_space", true);
		Material.SetShaderParameter("burn", burn);

		Capture(target);
		SyncGroupSpace();
	}

	public override void _Process(double delta)
	{
		SyncGroupSpace();
	}

	public override void _ExitTree()
	{
		Restore();
	}

	/// <summary>Runs the burn to completion over <paramref name="seconds"/>.</summary>
	public Tween Play(float seconds)
	{
		Tween tween = CreateTween();
		tween.TweenMethod(Callable.From<float>(value => Burn = value), 0.0f, 1.0f, seconds);
		return tween;
	}

	/// <summary>
	/// Puts the card back the way it was and removes this node. Only needed for a card that survives its
	/// burn; one that is freed afterwards is cleaned up by the tree.
	/// </summary>
	public void Clear()
	{
		Restore();
		QueueFree();
	}

	private void Capture(Node node)
	{
		// The effect node itself is not a CanvasItem, so it cannot catch its own material here.
		if (node is CanvasItem item)
		{
			replacedMaterials[item] = item.Material;
			item.Material = Material;
		}

		foreach (Node child in node.GetChildren())
		{
			Capture(child);
		}
	}

	private void Restore()
	{
		foreach (KeyValuePair<CanvasItem, Material> entry in replacedMaterials)
		{
			// A node can have been freed under us - the card's own code rebuilding its text, say - and
			// the burn is usually the last thing to happen to a card, so this is the common path.
			if (GodotObject.IsInstanceValid(entry.Key))
			{
				entry.Key.Material = entry.Value;
			}
		}

		replacedMaterials.Clear();
	}

	/// <summary>
	/// Tells the shader where the card is, so every node under it can turn its own global position into
	/// a position within the card. Inverted here rather than in the shader because it is one value for
	/// the whole subtree, and re-sent while the card moves so a card burning mid-flight - sliding out of
	/// a hand, scaling up into a modal - carries its burn front with it instead of being wiped through
	/// by one anchored to the screen.
	/// </summary>
	private void SyncGroupSpace()
	{
		if (target == null)
		{
			return;
		}

		Transform2D global = target.GetGlobalTransform();
		Vector2 size = target.Size;

		if (global == lastGlobalTransform && size == lastSize)
		{
			return;
		}

		lastGlobalTransform = global;
		lastSize = size;

		Transform2D inverse = global.AffineInverse();
		Material.SetShaderParameter("group_inv_x", inverse.X);
		Material.SetShaderParameter("group_inv_y", inverse.Y);
		Material.SetShaderParameter("group_inv_origin", inverse.Origin);
		Material.SetShaderParameter("group_size", size);
	}
}
