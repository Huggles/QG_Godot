using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Plays the shrink tween on a unit being removed, then calls CountryScene.RemoveUnit.
/// countryId must be captured before ExecuteAsync runs (UnitState.CountryId is -1 afterwards).
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

    public override async Task Execute()
    {
        CountryScene countryScene = CountryState.ForId(_countryId).CountryScene;
        UnitScene unitScene = UnitState.ForId(_unitId).UnitScene;

        if (unitScene == null) return;

        Tween tween = unitScene.CreateTween();
        tween.TweenProperty(unitScene.UnitSpriteNode, "scale", new Vector2(1.2f, 1.2f), GameSettings.AnimationDurationSeconds / 2);
        tween.TweenProperty(unitScene.UnitSpriteNode, "scale", new Vector2(0f, 0f), GameSettings.AnimationDurationSeconds / 2);
        await unitScene.ToSignal(tween, Tween.SignalName.Finished);
        tween.Dispose();

        countryScene.RemoveUnit(unitScene);
        unitScene.UnitSpriteNode.Scale = new Vector2(1f, 1f);

        
    }
}
