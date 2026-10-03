using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Drops a just-deployed unit onto the board: it falls in from above while its shadow darkens
/// underneath, then squashes briefly on landing. The unit is already positioned in the CountryScene.
/// </summary>
public class DeployUnitAnimation : ChangeEventAnimation
{
    /// <summary>How far above its spot the unit starts, in the unit scene's pixels.</summary>
    private const float DropHeight = 40f;

    /// <summary>Landing squash, as wider-by and shorter-by fractions of the sprite's scale.</summary>
    private const float Squash = 0.03f;

    private readonly int _unitId;
    private readonly int _countryId;

    public DeployUnitAnimation(int unitId, int countryId)
    {
        _unitId = unitId;
        _countryId = countryId;
    }

    protected override async Task AnimateForTargetFaction()
    {
        CountryScene countryScene = CountryState.ForId(_countryId).CountryScene;
        UnitScene unitScene = countryScene.AddUnit(_unitId);

        Sprite2D sprite = unitScene.UnitSpriteNode;
        Control shadow = sprite.GetNode<Control>("Shadow");
        Vector2 shadowGround = shadow.Position;
        float scale = UnitScene.DefaultSpriteScale;
        double fall = GameSettings.DurationVeryShortSeconds;
        double settle = fall / 2;

        // Start pose now, or the unit flashes on its spot for the frame before the tween's first step.
        SetFall(sprite, shadow, shadowGround, 0f);
        Tween tween = unitScene.CreateTween();
        tween.TweenMethod(Callable.From<float>(t => SetFall(sprite, shadow, shadowGround, t)), 0f, 1f, fall);
        tween.TweenProperty(sprite, "scale", new Vector2(scale * (1 + Squash), scale * (1 - Squash)), settle);
        tween.TweenProperty(sprite, "scale", new Vector2(scale, scale), settle);
        await unitScene.ToSignal(tween, Tween.SignalName.Finished);
        tween.Dispose();

        // Pinned rather than trusted to the tween's last frame, which a zero-length (fast-forward) run skips.
        SetFall(sprite, shadow, shadowGround, 1f);
        sprite.Scale = new Vector2(scale, scale);
    }

    /// <summary>
    /// One frame of the fall at progress <paramref name="t"/>, eased in like gravity. The shadow is the
    /// sprite's child, so it is pushed back down by the same height to stay on the ground.
    /// </summary>
    private static void SetFall(Sprite2D sprite, Control shadow, Vector2 shadowGround, float t)
    {
        float height = DropHeight * (1 - t * t);
        sprite.Position = new Vector2(0, -height);
        sprite.SelfModulate = new Color(sprite.SelfModulate, Mathf.Min(1f, t * 3f));
        shadow.Position = shadowGround + new Vector2(0, height / sprite.Scale.Y);
        shadow.Modulate = new Color(shadow.Modulate, t * t);
    }
}
