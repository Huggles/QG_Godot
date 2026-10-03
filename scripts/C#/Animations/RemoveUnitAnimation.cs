using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Burns away a unit being removed, the way a discarded card goes, then calls CountryScene.RemoveUnit.
/// countryId must be captured before Mutate runs (UnitState.CountryId is -1 afterwards).
/// The signal handler OnUnitRemovedFromCountry is a no-op so this animation owns scene cleanup.
/// </summary>
public class RemoveUnitAnimation : ChangeEventAnimation
{
    private readonly int _unitId;
    private readonly int _countryId;

    public RemoveUnitAnimation(int unitId, int countryId)
    {
        _unitId    = unitId;
        _countryId = countryId;
    }

    protected override async Task AnimateForTargetFaction()
    {
        CountryScene countryScene = CountryState.ForId(_countryId).CountryScene;
        UnitScene unitScene = UnitState.ForId(_unitId).UnitScene;

        if (unitScene == null) return;

        // The sprite is a lone Sprite2D, so the shader goes straight on it; BurnEffect's subtree
        // handling is only needed for Controls. Its shadow child fades out alongside instead.
        Sprite2D sprite = unitScene.UnitSpriteNode;
        CanvasItem shadow = sprite.GetNode<CanvasItem>("Shadow");
        Material previousMaterial = sprite.Material;
        ShaderMaterial burn = new ShaderMaterial { Shader = BurnEffect.BurnShader };
        // Blotches sized for the ~500px unit texture, and offset so units burning together differ.
        burn.SetShaderParameter("noise_scale_pixels", 120.0f);
        burn.SetShaderParameter("noise_offset", new Vector2(GD.Randf(), GD.Randf()) * 1000.0f);
        sprite.Material = burn;

        double seconds = GameSettings.DurationMediumSeconds;
        Tween tween = unitScene.CreateTween().SetParallel();
        tween.TweenMethod(Callable.From<float>(value => burn.SetShaderParameter("burn", value)), 0.0f, 1.0f, seconds);
        tween.TweenProperty(shadow, "modulate:a", 0.0f, seconds);
        await unitScene.ToSignal(tween, Tween.SignalName.Finished);
        tween.Dispose();

        // The scene is pooled and comes back on the next deploy, so it has to come back unburnt.
        sprite.Material = previousMaterial;
        shadow.Modulate = new Color(shadow.Modulate, 1.0f);

        countryScene.RemoveUnit(unitScene);
    }
}
