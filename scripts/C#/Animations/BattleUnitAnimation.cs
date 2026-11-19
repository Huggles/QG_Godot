using Godot;
using System.Threading.Tasks;

/// <summary>A unit lost in battle: an army is shelled and burns, a navy is bracketed by splashes and sinks.</summary>
public class BattleUnitAnimation : RemoveUnitAnimation
{
    private static readonly Shader SinkShader = GD.Load<Shader>("res://assets/materials/shaders/Sink.gdshader");

    public BattleUnitAnimation(int unitId, int countryId) : base(unitId, countryId) { }

    protected override Task Destroy(UnitScene unitScene)
    {
        // Skipped pacing (save restore, headless) gets the plain zero-length burn.
        if (BattleFx.Pace <= 0f) return base.Destroy(unitScene);
        return unitScene.IsNavy ? Sink(unitScene) : Shell(unitScene);
    }

    private static async Task Shell(UnitScene unitScene)
    {
        Sprite2D sprite = unitScene.UnitSpriteNode;
        Vector2 center = sprite.GlobalPosition;
        Vector2 half = PieceHalfSize(sprite);
        float size = half.X / 50f;

        for (int i = 0; i < 2; i++)
        {
            BattleFx.Impact(center + Vector2.FromAngle(GD.Randf() * Mathf.Tau) * half.X * (float)GD.RandRange(0.8, 1.3), size * 0.7f);
            await BattleFx.Wait(unitScene, 0.2);
        }
        BattleFx.Impact(center, size);

        CpuParticles2D embers = BattleFx.Embers(center, half * 0.8f);
        await Burn(unitScene, GameSettings.DurationMediumSeconds);
        BattleFx.StopAndFree(embers);
        BattleFx.LingeringSmoke(center + new Vector2(0, half.Y * 0.3f), size, 2.5);
    }

    private static async Task Sink(UnitScene unitScene)
    {
        Sprite2D sprite = unitScene.UnitSpriteNode;
        CanvasItem shadow = sprite.GetNode<CanvasItem>("Shadow");
        Vector2 center = sprite.GlobalPosition;
        Vector2 half = PieceHalfSize(sprite);
        float size = half.X / 50f;
        float side = GD.Randf() < 0.5f ? -1f : 1f;

        BattleFx.Splash(center + new Vector2(side * half.X * 1.2f, half.Y * 0.4f), size * 0.8f);
        await BattleFx.Wait(unitScene, 0.25);
        BattleFx.Splash(center + new Vector2(-side * half.X * 0.9f, -half.Y * 0.5f), size * 0.8f);
        await BattleFx.Wait(unitScene, 0.25);
        BattleFx.Impact(center, size * 0.8f, dirt: false);
        await BattleFx.Wait(unitScene, 0.15);

        Material previousMaterial = sprite.Material;
        Vector2 previousPosition = sprite.Position;
        float previousRotation = sprite.Rotation;
        ShaderMaterial sink = new ShaderMaterial { Shader = SinkShader };
        sprite.Material = sink;

        double seconds = 1.3 * BattleFx.Pace;
        CpuParticles2D bubbles = BattleFx.Bubbles(center + new Vector2(0, half.Y * 0.5f), half.X * 0.6f);
        BattleFx.Ripple(center + new Vector2(0, half.Y * 0.4f), size * 1.2f, 1.4f);
        BattleFx.LingeringSmoke(center, size * 0.7f, 1.0);

        Tween tween = unitScene.CreateTween().SetParallel();
        tween.TweenMethod(Callable.From<float>(value => sink.SetShaderParameter("sink", value)), 0f, 1f, seconds).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
        tween.TweenProperty(sprite, "rotation", previousRotation + side * 0.22f, seconds).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "position:y", previousPosition.Y + half.Y * 0.3f, seconds).SetEase(Tween.EaseType.In);
        tween.TweenProperty(shadow, "modulate:a", 0f, seconds * 0.5);
        await unitScene.ToSignal(tween, Tween.SignalName.Finished);
        tween.Dispose();
        BattleFx.StopAndFree(bubbles);
        BattleFx.Ripple(center + new Vector2(0, half.Y * 0.4f), size * 0.9f, 1.0f);

        sprite.Material = previousMaterial;
        sprite.Position = previousPosition;
        sprite.Rotation = previousRotation;
        shadow.Modulate = new Color(shadow.Modulate, 1f);
    }

    private static Vector2 PieceHalfSize(Sprite2D sprite) => sprite.GetRect().Size * sprite.GlobalScale.Abs() / 2f;
}
