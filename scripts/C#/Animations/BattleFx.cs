using Godot;
using System.Threading.Tasks;

/// <summary>
/// Battle particles and camera shake, built from gradients in code so there are no textures to import.
/// Sizes are in units of one piece (~100 world px); every effect frees itself.
/// </summary>
public static class BattleFx
{
    private const int FxZIndex = 70;

    /// <summary>1 at Normal speed, smaller when faster, 0 when pacing is skipped (restore, headless).</summary>
    public static float Pace => (float)GameSettings.DurationMediumSeconds;

    private static Texture2D _softDot;
    private static Texture2D _ring;
    private static readonly CanvasItemMaterial Additive = new() { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
    private static Texture2D SoftDot =>_softDot ??= Radial(Grad((0f, new Color(1, 1, 1, 1)), (0.35f, new Color(1, 1, 1, 0.7f)), (1f, new Color(1, 1, 1, 0))));
    private static Texture2D Ring => _ring ??= Radial(Grad((0f, new Color(1, 1, 1, 0)), (0.68f, new Color(1, 1, 1, 0)), (0.84f, new Color(1, 1, 1, 1)), (1f, new Color(1, 1, 1, 0))));

    public static async Task Wait(Node node, double seconds) =>
        await node.ToSignal(node.GetTree().CreateTimer(seconds * Pace), SceneTreeTimer.SignalName.Timeout);

    /// <summary>Shell hit: flash, fire, flying dirt and a smoke puff.</summary>
    public static void Impact(Vector2 at, float size = 1f, bool dirt = true)
    {
        Smoke(at, size, 8, 1.4f);
        Burst(at, new CpuParticles2D
        {
            Amount = 1, Lifetime = 0.2f, Material = Additive, Gravity = Vector2.Zero,
            ScaleAmountMin = 2.4f * size, ScaleAmountMax = 2.4f * size, ScaleAmountCurve = Fade(1f, 0.3f),
            ColorRamp = Grad((0f, new Color(1f, 0.97f, 0.85f, 1f)), (1f, new Color(1f, 0.8f, 0.4f, 0f))),
        });
        Burst(at, new CpuParticles2D
        {
            Amount = 22, Lifetime = 0.55f, Spread = 180f, Gravity = new Vector2(0, -60), Material = Additive,
            InitialVelocityMin = 60f * size, InitialVelocityMax = 210f * size, DampingMin = 250f, DampingMax = 350f,
            ScaleAmountMin = 0.35f * size, ScaleAmountMax = 0.85f * size, ScaleAmountCurve = Fade(1f, 0.2f),
            ColorRamp = Grad((0f, new Color(1f, 0.9f, 0.6f)), (0.3f, new Color(1f, 0.45f, 0.1f)), (0.7f, new Color(0.4f, 0.08f, 0f)), (1f, new Color(0f, 0f, 0f, 0f))),
        });
        if (dirt)
            Burst(at, new CpuParticles2D
            {
                Amount = 12, Lifetime = 0.9f, Direction = Vector2.Up, Spread = 65f, Gravity = new Vector2(0, 750),
                InitialVelocityMin = 160f * size, InitialVelocityMax = 340f * size,
                ScaleAmountMin = 0.08f * size, ScaleAmountMax = 0.18f * size,
                ColorRamp = Grad((0f, new Color(0.25f, 0.2f, 0.14f)), (0.8f, new Color(0.2f, 0.16f, 0.12f)), (1f, new Color(0.2f, 0.16f, 0.12f, 0f))),
            });
        Shake(7f * size, 0.25f);
    }

    /// <summary>A white column of water thrown up by a near miss, with a ripple at its foot.</summary>
    public static void Splash(Vector2 at, float size = 1f)
    {
        Burst(at, new CpuParticles2D
        {
            Amount = 28, Lifetime = 0.85f, Direction = Vector2.Up, Spread = 10f, Gravity = new Vector2(0, 900),
            InitialVelocityMin = 220f * size, InitialVelocityMax = 430f * size,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle, EmissionRectExtents = new Vector2(8f * size, 2f),
            ScaleAmountMin = 0.12f * size, ScaleAmountMax = 0.32f * size,
            ColorRamp = Grad((0f, new Color(1f, 1f, 1f, 0.95f)), (0.6f, new Color(0.75f, 0.88f, 1f, 0.8f)), (1f, new Color(0.6f, 0.8f, 1f, 0f))),
        });
        Burst(at, new CpuParticles2D
        {
            Amount = 10, Lifetime = 0.7f, Direction = Vector2.Up, Spread = 70f, Gravity = new Vector2(0, 200),
            InitialVelocityMin = 50f * size, InitialVelocityMax = 140f * size, DampingMin = 80f, DampingMax = 120f,
            ScaleAmountMin = 0.35f * size, ScaleAmountMax = 0.6f * size, ScaleAmountCurve = Fade(0.6f, 1.3f),
            ColorRamp = Grad((0f, new Color(1f, 1f, 1f, 0.45f)), (1f, new Color(0.85f, 0.93f, 1f, 0f))),
        });
        Ripple(at, size * 0.8f, 0.9f);
        Shake(3f * size, 0.15f);
    }

    /// <summary>An expanding ring that fades out: water rings for ships, a dust shockwave on land.</summary>
    public static void Ripple(Vector2 at, float size, float seconds, Color? color = null)
    {
        Sprite2D ring = new Sprite2D { Texture = Ring, ZIndex = FxZIndex - 1, Modulate = color ?? new Color(0.88f, 0.96f, 1f, 0.85f) };
        ring.Scale = Vector2.One * 0.3f * size;
        Add(ring, at);
        Tween tween = ring.CreateTween().SetParallel();
        tween.TweenProperty(ring, "scale", Vector2.One * 2.6f * size, seconds * Pace).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(ring, "modulate:a", 0f, seconds * Pace).SetEase(Tween.EaseType.In);
        tween.Chain().TweenCallback(Callable.From(ring.QueueFree));
    }

    /// <summary>Dust kicked outward in a ring, for a battle in an empty land space.</summary>
    public static void DustRing(Vector2 at, float size = 1f)
    {
        Burst(at, new CpuParticles2D
        {
            Amount = 28, Lifetime = 1.1f, Spread = 180f, Gravity = new Vector2(0, -15),
            InitialVelocityMin = 170f * size, InitialVelocityMax = 260f * size, DampingMin = 220f, DampingMax = 260f,
            ScaleAmountMin = 0.5f * size, ScaleAmountMax = 0.9f * size, ScaleAmountCurve = Fade(0.6f, 1.5f),
            ColorRamp = Grad((0f, new Color(0.72f, 0.62f, 0.45f, 0.65f)), (1f, new Color(0.6f, 0.52f, 0.4f, 0f))),
        });
        Ripple(at, size * 1.4f, 0.8f, new Color(1f, 0.9f, 0.7f, 0.7f));
    }

    /// <summary>Sparks rising off a burning piece; stop it with <see cref="StopAndFree"/>.</summary>
    public static CpuParticles2D Embers(Vector2 at, Vector2 extents) => Stream(at, new CpuParticles2D
    {
        Amount = 36, Lifetime = 1.0f, Direction = Vector2.Up, Spread = 25f, Gravity = new Vector2(0, -50), Material = Additive,
        EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle, EmissionRectExtents = extents,
        InitialVelocityMin = 20f, InitialVelocityMax = 70f,
        ScaleAmountMin = 0.05f, ScaleAmountMax = 0.13f, ScaleAmountCurve = Fade(1f, 0.2f),
        ColorRamp = Grad((0f, new Color(1f, 0.9f, 0.5f)), (0.5f, new Color(1f, 0.45f, 0.08f)), (1f, new Color(0.6f, 0.1f, 0.0f, 0f))),
    });

    /// <summary>Air bubbles over a sinking ship; stop it with <see cref="StopAndFree"/>.</summary>
    public static CpuParticles2D Bubbles(Vector2 at, float width) => Stream(at, new CpuParticles2D
    {
        Amount = 18, Lifetime = 0.9f, Texture = Ring, Direction = Vector2.Up, Spread = 15f, Gravity = new Vector2(0, -30),
        EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle, EmissionRectExtents = new Vector2(width, 4f),
        InitialVelocityMin = 20f, InitialVelocityMax = 55f,
        ScaleAmountMin = 0.08f, ScaleAmountMax = 0.18f,
        ColorRamp = Grad((0f, new Color(0.9f, 0.97f, 1f, 0.9f)), (1f, new Color(0.8f, 0.92f, 1f, 0f))),
    });

    /// <summary>A plume that keeps rising from a destroyed piece for a while after it has gone.</summary>
    public static void LingeringSmoke(Vector2 at, float size, double seconds)
    {
        CpuParticles2D smoke = Stream(at, new CpuParticles2D
        {
            Amount = 26, Lifetime = 2.6f, Direction = Vector2.Up, Spread = 15f, Gravity = new Vector2(14, -25),
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle, EmissionRectExtents = new Vector2(14f * size, 4f),
            InitialVelocityMin = 35f * size, InitialVelocityMax = 60f * size,
            ScaleAmountMin = 0.35f * size, ScaleAmountMax = 0.6f * size, ScaleAmountCurve = Fade(0.5f, 2.4f),
            ColorRamp = Grad((0f, new Color(0.15f, 0.14f, 0.13f, 0f)), (0.12f, new Color(0.22f, 0.21f, 0.2f, 0.42f)), (1f, new Color(0.5f, 0.49f, 0.48f, 0f))),
        });
        smoke.GetTree().CreateTimer(seconds * Pace).Timeout += () => StopAndFree(smoke);
    }

    public static void StopAndFree(CpuParticles2D particles)
    {
        if (!GodotObject.IsInstanceValid(particles)) return;
        particles.Emitting = false;
        particles.GetTree().CreateTimer(particles.Lifetime / particles.SpeedScale + 0.1).Timeout += particles.QueueFree;
    }

    /// <summary>Jitters the camera offset, decaying to rest.</summary>
    public static void Shake(float strength, float seconds)
    {
        Camera2D camera = InputManager.Current?.Camera;
        if (camera == null) return;
        Tween tween = camera.CreateTween();
        tween.TweenMethod(Callable.From<float>(left => camera.Offset = new Vector2((float)GD.RandRange(-1.0, 1.0), (float)GD.RandRange(-1.0, 1.0)) * strength * left),
            1f, 0f, seconds * Pace);
        tween.TweenCallback(Callable.From(() => camera.Offset = Vector2.Zero));
    }

    private static void Smoke(Vector2 at, float size, int amount, float lifetime) => Burst(at, new CpuParticles2D
    {
        Amount = amount, Lifetime = lifetime, Spread = 180f, Gravity = new Vector2(8, -35),
        InitialVelocityMin = 20f * size, InitialVelocityMax = 60f * size, DampingMin = 20f, DampingMax = 30f,
        ScaleAmountMin = 0.6f * size, ScaleAmountMax = 1.1f * size, ScaleAmountCurve = Fade(0.4f, 1.5f),
        ColorRamp = Grad((0f, new Color(0.3f, 0.28f, 0.26f, 0.7f)), (1f, new Color(0.5f, 0.48f, 0.46f, 0f))),
    });

    private static void Burst(Vector2 at, CpuParticles2D particles)
    {
        particles.OneShot = true;
        particles.Explosiveness = 1f;
        Start(particles, at);
        particles.GetTree().CreateTimer(particles.Lifetime / particles.SpeedScale + 0.2).Timeout += particles.QueueFree;
    }

    private static CpuParticles2D Stream(Vector2 at, CpuParticles2D particles)
    {
        Start(particles, at);
        return particles;
    }

    private static void Start(CpuParticles2D particles, Vector2 at)
    {
        particles.Texture ??= SoftDot;
        particles.ZIndex = FxZIndex;
        particles.SpeedScale = 1f / Mathf.Max(Pace, 0.1f);
        Add(particles, at);
        particles.Emitting = true;
    }

    private static void Add(Node2D node, Vector2 at)
    {
        NodeUtilities.Instance.UnitsNode.AddChild(node);
        node.GlobalPosition = at;
    }

    private static Curve Fade(float from, float to)
    {
        Curve curve = new Curve { MaxValue = Mathf.Max(from, to) };
        curve.AddPoint(new Vector2(0, from));
        curve.AddPoint(new Vector2(1, to));
        return curve;
    }

    private static Gradient Grad(params (float offset, Color color)[] stops)
    {
        Gradient gradient = new Gradient();
        gradient.Offsets = System.Array.ConvertAll(stops, stop => stop.offset);
        gradient.Colors = System.Array.ConvertAll(stops, stop => stop.color);
        return gradient;
    }

    private static Texture2D Radial(Gradient gradient) => new GradientTexture2D
    {
        Gradient = gradient, Width = 64, Height = 64, Fill = GradientTexture2D.FillEnum.Radial,
        FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
    };
}
