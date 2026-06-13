using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Plays the scale-pop tween on a unit that was just deployed to a country.
/// The unit is already positioned in the CountryScene by the time this runs.
/// </summary>
public class DeployUnitAnimation : ChangeEventAnimation
{
    private readonly int _unitId;
    private readonly int _countryId;

    public DeployUnitAnimation(int unitId, int countryId)
    {
        _unitId = unitId;
        _countryId = countryId;
    }

    public override async Task Execute()
    {
        CountryScene countryScene = CountryState.ForId(_countryId).CountryScene;

        UnitScene unitScene = new[] { countryScene.UnitScene1, countryScene.UnitScene2, countryScene.UnitScene3 }
            .FirstOrDefault(u => u?.UnitId == _unitId);

        if (unitScene == null) throw new DeployUnitAnimationException($"DeployUnitAnimation: Could not find unit {_unitId} in country {_countryId} scene.");

        unitScene.AddChild(unitScene);

        Tween tween = unitScene.CreateTween();
        tween.TweenProperty(unitScene.UnitSpriteNode, "scale", new Vector2(1.5f, 1.5f), GameSettings.AnimationDurationSeconds / 2);
        tween.TweenProperty(unitScene.UnitSpriteNode, "scale", new Vector2(1f, 1f), GameSettings.AnimationDurationSeconds / 2);
        await unitScene.ToSignal(tween, Tween.SignalName.Finished);
        tween.Dispose();
    }
}
